using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.CorpusImporter.Models;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using CorpusHttpClient = global::KnowledgeHub.CorpusImporter.Services.CorpusImporter;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KnowledgeHub.Tests.Integration;

public sealed class IncrementalCorpusImportIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _http;

    public IncrementalCorpusImportIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _http = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public Task InitializeAsync() => _factory.ResetDocumentsAsync();

    public Task DisposeAsync()
    {
        _http.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Reimport_WithStableGuid_KeepsDocumentChunksAndEmbeddings()
    {
        var externalId = Guid.NewGuid();
        var corpus = One(externalId, "Stable name", "Identical content");
        var importer = new CorpusHttpClient(_http);
        Assert.Equal(1, (await importer.ImportAsync(corpus)).Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var stored = await db.Documents.AsNoTracking().SingleAsync(
            x => x.ExternalId == externalId);
        var chunkId = await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == stored.Id).Select(x => x.Id).SingleAsync();
        var embeddingId = await db.ChunkEmbeddings.AsNoTracking()
            .Where(x => x.ChunkId == chunkId).Select(x => x.Id).SingleAsync();
        var updatedAt = stored.UpdatedAt;

        var second = await importer.ImportAsync(corpus);
        Assert.Equal(1, second.Unchanged);
        Assert.Equal(0, second.UpdatedContent);
        Assert.Equal(0, second.UpdatedMetadata);
        Assert.Equal(chunkId, await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == stored.Id).Select(x => x.Id).SingleAsync());
        Assert.Equal(embeddingId, await db.ChunkEmbeddings.AsNoTracking()
            .Where(x => x.ChunkId == chunkId).Select(x => x.Id).SingleAsync());
        Assert.Equal(updatedAt, (await db.Documents.AsNoTracking()
            .SingleAsync(x => x.Id == stored.Id)).UpdatedAt);
        Assert.Equal(KnowledgeDocument.ComputeContentSha256("Identical content"),
            stored.ContentSha256);
    }

    [Fact]
    public async Task ChangedContent_ReindexesAtomicallyAndKeepsInternalIdentity()
    {
        var externalId = Guid.NewGuid();
        var importer = new CorpusHttpClient(_http);
        await importer.ImportAsync(One(externalId, "Same name", "Old content"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var oldDocument = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.ExternalId == externalId);
        var oldChunkId = await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == oldDocument.Id).Select(x => x.Id).SingleAsync();

        var report = await importer.ImportAsync(One(
            externalId, "Same name", "Different content after correction"));
        Assert.Equal(1, report.UpdatedContent);

        var updated = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.ExternalId == externalId);
        Assert.Equal(oldDocument.Id, updated.Id);
        Assert.Equal("Different content after correction", updated.Content);
        Assert.Equal(KnowledgeDocument.ComputeContentSha256(updated.Content),
            updated.ContentSha256);
        Assert.False(await db.DocumentChunks.AsNoTracking()
            .AnyAsync(x => x.Id == oldChunkId));
        var replacement = await db.DocumentChunks.AsNoTracking()
            .SingleAsync(x => x.DocumentId == updated.Id);
        Assert.True(await db.ChunkEmbeddings.AsNoTracking()
            .AnyAsync(x => x.ChunkId == replacement.Id));
    }

    [Fact]
    public async Task RenamingAndMetadataChanges_DoNotRegenerateEmbeddings()
    {
        var externalId = Guid.NewGuid();
        var importer = new CorpusHttpClient(_http);
        var original = One(externalId, "Old title", "Unchanged text");
        await importer.ImportAsync(original);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var search = scope.ServiceProvider.GetRequiredService<IVectorSearchService>();
        var before = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.ExternalId == externalId);
        var chunkId = await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == before.Id).Select(x => x.Id).SingleAsync();
        var embeddingId = await db.ChunkEmbeddings.AsNoTracking()
            .Where(x => x.ChunkId == chunkId).Select(x => x.Id).SingleAsync();
        Assert.Equal("Old title", Assert.Single(await search.SearchAsync("query")).Title);

        var marker = Guid.NewGuid().ToString("N");
        var updated = One(externalId, "New title", "Unchanged text");
        updated.Documents[0].Summary = "A new summary";
        updated.Documents[0].SearchKeywords = "NewKeywords";
        updated.Documents[0].CategoryName = "Category-" + marker;
        updated.Documents[0].SourceName = "Source-" + marker;
        updated.Documents[0].TagNames = ["Tag-" + marker];
        updated.Categories = [new CorpusCategory { Name = "Category-" + marker }];
        updated.Sources = [new CorpusSource
        {
            Name = "Source-" + marker, Url = "https://example.org/" + marker
        }];
        updated.Tags = [new CorpusTag { Name = "Tag-" + marker }];

        var report = await importer.ImportAsync(updated);
        Assert.Equal(1, report.UpdatedMetadata);
        Assert.Equal(0, report.UpdatedContent);
        Assert.Equal(chunkId, await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == before.Id).Select(x => x.Id).SingleAsync());
        Assert.Equal(embeddingId, await db.ChunkEmbeddings.AsNoTracking()
            .Where(x => x.ChunkId == chunkId).Select(x => x.Id).SingleAsync());
        var after = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.Id == before.Id);
        Assert.Equal("New title", after.Title);
        Assert.Equal("A new summary", after.Summary);
        Assert.Equal("NewKeywords", after.SearchKeywords);
        Assert.NotNull(after.CategoryId);
        Assert.NotNull(after.SourceId);
        Assert.True(await db.Set<KnowledgeDocumentTag>().AsNoTracking()
            .AnyAsync(x => x.DocumentId == after.Id));
        Assert.Equal("New title", Assert.Single(await search.SearchAsync("query")).Title);
    }

    [Fact]
    public async Task DifferentExternalIds_WithIdenticalTitles_RemainDifferentDocuments()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var corpus = new CorpusRoot
        {
            Documents =
            [
                new CorpusDocument { Id = firstId, Title = "Shared title", Content = "A" },
                new CorpusDocument { Id = secondId, Title = "Shared title", Content = "B" }
            ]
        };
        Assert.Equal(2, (await new CorpusHttpClient(_http).ImportAsync(corpus)).Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        Assert.Equal(2, await db.Documents.AsNoTracking()
            .CountAsync(x => x.Title == "Shared title"));
        Assert.Equal(2, await db.Documents.AsNoTracking()
            .Where(x => x.Title == "Shared title")
            .Select(x => x.ExternalId).Distinct().CountAsync());
    }

    [Fact]
    public async Task DuplicateExplicitGuid_IsRejectedBeforeAnyWrite()
    {
        var id = Guid.NewGuid();
        var corpus = new CorpusRoot
        {
            Documents =
            [
                new CorpusDocument { Id = id, Title = "First", Content = "A" },
                new CorpusDocument { Id = id, Title = "Second", Content = "B" }
            ]
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new CorpusHttpClient(_http).ImportAsync(corpus));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        Assert.Equal(0, await db.Documents.CountAsync());
    }

    [Fact]
    public async Task LegacyTitleLookup_DoesNotAdoptAnExternalIdentity()
    {
        var externalId = Guid.NewGuid();
        var importer = new CorpusHttpClient(_http);
        Assert.Equal(1, (await importer.ImportAsync(
            One(externalId, "Shared title", "Externally managed"))).Created);

        var legacy = new CorpusRoot
        {
            Documents =
            [
                new CorpusDocument
                {
                    Title = "Shared title",
                    Content = "Legacy content"
                }
            ]
        };

        Assert.Equal(1, (await importer.ImportAsync(legacy)).Created);
        Assert.Equal(1, (await importer.ImportAsync(legacy)).Unchanged);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var documents = await db.Documents.AsNoTracking()
            .Where(x => x.Title == "Shared title")
            .ToArrayAsync();
        Assert.Equal(2, documents.Length);
        Assert.Contains(documents, x => x.ExternalId == externalId &&
                                        x.Content == "Externally managed");
        Assert.Contains(documents, x => x.ExternalId is null &&
                                        x.Content == "Legacy content");
    }

    [Fact]
    public async Task FailedReembedding_DoesNotOverwritePersistedContent()
    {
        var id = Guid.NewGuid();
        var importer = new CorpusHttpClient(_http);
        await importer.ImportAsync(One(id, "Stable", "Safe content"));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var before = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.ExternalId == id);
        var oldChunk = await db.DocumentChunks.AsNoTracking()
            .SingleAsync(x => x.DocumentId == before.Id);

        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmbeddingService>();
                services.AddScoped<IEmbeddingService, FailOnMarkerEmbeddingService>();
            }));
        using var failingClient = failingFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new CorpusHttpClient(failingClient).ImportAsync(
                One(id, "Changed", "FAIL simulated embedding")));

        var after = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.ExternalId == id);
        Assert.Equal("Safe content", after.Content);
        Assert.Equal("Stable", after.Title);
        Assert.True(await db.DocumentChunks.AsNoTracking()
            .AnyAsync(x => x.Id == oldChunk.Id));
        Assert.True(await db.ChunkEmbeddings.AsNoTracking()
            .AnyAsync(x => x.ChunkId == oldChunk.Id));
    }

    private static CorpusRoot One(Guid id, string title, string content) => new()
    {
        Documents = [new CorpusDocument { Id = id, Title = title, Content = content }]
    };
}
