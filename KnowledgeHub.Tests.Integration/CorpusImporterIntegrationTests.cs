using System.Net;
using System.Net.Http.Json;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.CorpusImporter.Models;
using CorpusHttpClient = global::KnowledgeHub.CorpusImporter.Services.CorpusImporter;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KnowledgeHub.Tests.Integration;

public sealed class CorpusImporterIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>, IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _http;

    public CorpusImporterIntegrationTests(CustomWebApplicationFactory factory)
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
    public async Task Import_ThroughHttp_PersistsMetadataIndexAndRefreshesApiCache()
    {
        using var scope = _factory.Services.CreateScope();
        var search = scope.ServiceProvider.GetRequiredService<IVectorSearchService>();
        Assert.Empty(await search.SearchAsync("question"));

        var marker = Guid.NewGuid().ToString("N");
        var content = new string('A', 620);
        var corpus = new CorpusRoot
        {
            Categories = [new CorpusCategory { Name = "Category-" + marker }],
            Sources = [new CorpusSource { Name = "Source-" + marker, Url = "https://example.org" }],
            Tags = [new CorpusTag { Name = "Tag-" + marker }],
            Documents = [new CorpusDocument
            {
                Title = "Document-" + marker,
                Content = content,
                Summary = "Résumé indexé",
                SearchKeywords = "alpha, beta",
                CategoryName = "Category-" + marker,
                SourceName = "Source-" + marker,
                TagNames = ["Tag-" + marker]
            }]
        };

        var report = await new CorpusHttpClient(_http).ImportAsync(corpus);
        Assert.Equal(1, report.Created);
        Assert.Equal(1, report.CategoriesCreated);
        Assert.Equal(1, report.SourcesCreated);
        Assert.Equal(1, report.TagsCreated);

        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var document = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.Title == "Document-" + marker);
        Assert.NotNull(document.CategoryId);
        Assert.NotNull(document.SourceId);
        Assert.Equal("Résumé indexé", document.Summary);
        Assert.Equal("alpha, beta", document.SearchKeywords);
        Assert.True(await db.Set<KnowledgeDocumentTag>()
            .AnyAsync(x => x.DocumentId == document.Id));

        var chunks = await db.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == document.Id)
            .OrderBy(x => x.ChunkIndex)
            .ToArrayAsync();
        Assert.Equal(2, chunks.Length);
        Assert.Equal(new[] { 0, 1 }, chunks.Select(x => x.ChunkIndex));
        var chunkIds = chunks.Select(x => x.Id).ToArray();
        Assert.Equal(2, await db.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => chunkIds.Contains(x.ChunkId)));

        var hit = Assert.Single(await search.SearchAsync("question"));
        Assert.Equal(document.Id, hit.DocumentId);
    }

    [Fact]
    public async Task Reimport_ThroughHttp_DoesNotModifyCompleteIndex()
    {
        var title = "Idempotence-" + Guid.NewGuid().ToString("N");
        var corpus = OneDocument(title, "One indexed document");
        var importer = new CorpusHttpClient(_http);

        var first = await importer.ImportAsync(corpus);
        Assert.Equal(1, first.Created);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        var chunkId = await db.DocumentChunks.AsNoTracking()
            .Where(x => x.Document.Title == title)
            .Select(x => x.Id).SingleAsync();

        var second = await importer.ImportAsync(corpus);
        Assert.Equal(0, second.Created);
        Assert.Equal(0, second.Repaired);
        Assert.Equal(1, second.Unchanged);

        Assert.Equal(chunkId, await db.DocumentChunks.AsNoTracking()
            .Where(x => x.Document.Title == title)
            .Select(x => x.Id).SingleAsync());
        Assert.Equal(1, await db.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => x.Chunk.Document.Title == title));
    }

    [Fact]
    public async Task Reimport_ThroughHttp_RepairsLegacyAndPreservesEditedContent()
    {
        var title = "Legacy-" + Guid.NewGuid().ToString("N");
        var document = new KnowledgeDocument(title, "Existing user edited content");
        document.AddChunk(0, 12, "Orphan chunk");
        var oldChunkId = Assert.Single(document.Chunks).Id;

        using (var scope = _factory.Services.CreateScope())
        {
            var setup = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
            await setup.Documents.AddAsync(document);
            await setup.SaveChangesAsync();
        }

        var report = await new CorpusHttpClient(_http).ImportAsync(
            OneDocument(title, "Old corpus content"));
        Assert.Equal(1, report.Repaired);

        using var verificationScope = _factory.Services.CreateScope();
        var db = verificationScope.ServiceProvider
            .GetRequiredService<KnowledgeDbContext>();
        var stored = await db.Documents.AsNoTracking()
            .SingleAsync(x => x.Id == document.Id);
        Assert.Equal("Existing user edited content", stored.Content);
        Assert.False(await db.DocumentChunks.AnyAsync(x => x.Id == oldChunkId));
        var chunk = await db.DocumentChunks.AsNoTracking()
            .SingleAsync(x => x.DocumentId == document.Id);
        Assert.Equal(stored.Content, chunk.Content);
        Assert.True(await db.ChunkEmbeddings.AsNoTracking()
            .AnyAsync(x => x.ChunkId == chunk.Id));
    }

    [Fact]
    public async Task EmbeddingFailure_ViaHttp_IsAtomicPerDocumentAndCanResume()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var firstTitle = "Good-" + suffix;
        var failedTitle = "Failure-" + suffix;
        var corpus = new CorpusRoot
        {
            Documents =
            [
                new CorpusDocument { Title = firstTitle, Content = "Valid content" },
                new CorpusDocument
                {
                    Title = failedTitle,
                    Content = new string('Q', 500) + "FAIL generation"
                }
            ]
        };

        using var failingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IEmbeddingService>();
                services.AddScoped<IEmbeddingService, FailOnMarkerEmbeddingService>();
            }));
        using var failingClient = failingFactory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new CorpusHttpClient(failingClient).ImportAsync(corpus));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeDbContext>();
        Assert.True(await db.Documents.AsNoTracking()
            .AnyAsync(x => x.Title == firstTitle));
        Assert.False(await db.Documents.AsNoTracking()
            .AnyAsync(x => x.Title == failedTitle));

        var resumed = await new CorpusHttpClient(_http).ImportAsync(corpus);
        Assert.Equal(1, resumed.Created);
        Assert.Equal(1, resumed.Unchanged);
        Assert.Equal(2, await db.Documents.AsNoTracking()
            .CountAsync(x => x.Title == firstTitle || x.Title == failedTitle));
        Assert.Equal(3, await db.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => x.Chunk.Document.Title == firstTitle ||
                             x.Chunk.Document.Title == failedTitle));
    }

    [Fact]
    public async Task EnsureIndex_UnknownDocument_Returns404()
    {
        using var response = await _http.PostAsJsonAsync(
            $"/api/documents/{Guid.NewGuid():D}/ensure-index", new { });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static CorpusRoot OneDocument(string title, string content) => new()
    {
        Documents = [new CorpusDocument { Title = title, Content = content }]
    };
}

public sealed class FailOnMarkerEmbeddingService : IEmbeddingService
{
    private readonly DeterministicEmbeddingService _inner = new();

    public Task<float[]> GenerateEmbeddingAsync(
        string text, CancellationToken cancellationToken = default)
    {
        if (text.Contains("FAIL", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Synthetic embedding failure.");
        }

        return _inner.GenerateEmbeddingAsync(text, cancellationToken);
    }
}
