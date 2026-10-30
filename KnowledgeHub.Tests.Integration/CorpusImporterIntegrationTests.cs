using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.CorpusImporter.Models;
using CorpusImporterService = global::KnowledgeHub.CorpusImporter.Services.CorpusImporter;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KnowledgeHub.Tests.Integration;

public sealed class CorpusImporterIntegrationTests
    : IClassFixture<IsolatedPostgresFixture>, IAsyncLifetime
{
    private readonly IsolatedPostgresFixture _database;

    public CorpusImporterIntegrationTests(IsolatedPostgresFixture database)
    {
        _database = database;
    }

    public async Task InitializeAsync()
    {
        await using var context = _database.CreateContext();
        await context.Documents.ExecuteDeleteAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Import_PersistsDocumentChunksEmbeddingsAndRefreshesLocalCache()
    {
        await using var context = _database.CreateContext();
        await using var searchContext = _database.CreateContext();
        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new VectorIndexCache(memory);
        var embeddings = new DeterministicEmbeddingService();
        var search = new VectorSearchService(searchContext, embeddings, cache);
        Assert.Empty(await search.SearchAsync("question"));

        var marker = Guid.NewGuid().ToString("N");
        var content = new string('A', 620);
        var corpus = new CorpusRoot
        {
            Categories = [new CorpusCategory { Name = "Category-" + marker }],
            Sources = [new CorpusSource { Name = "Source-" + marker }],
            Tags = [new CorpusTag { Name = "Tag-" + marker }],
            Documents = [new CorpusDocument
            {
                Title = "Document-" + marker,
                Content = content,
                CategoryName = "Category-" + marker,
                SourceName = "Source-" + marker,
                TagNames = ["Tag-" + marker]
            }]
        };

        await NewImporter(context, embeddings, cache).ImportAsync(corpus);

        var document = await context.Documents.AsNoTracking()
            .SingleAsync(x => x.Title == "Document-" + marker);
        var chunks = await context.DocumentChunks.AsNoTracking()
            .Where(x => x.DocumentId == document.Id)
            .OrderBy(x => x.ChunkIndex)
            .ToListAsync();
        Assert.Equal(2, chunks.Count);
        Assert.Equal(new[] { 0, 1 }, chunks.Select(x => x.ChunkIndex).ToArray());
        var chunkIds = chunks.Select(x => x.Id).ToArray();
        Assert.Equal(2, await context.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => chunkIds.Contains(x.ChunkId)));
        Assert.NotNull(document.CategoryId);
        Assert.NotNull(document.SourceId);

        var hit = Assert.Single(await search.SearchAsync("question"));
        Assert.Equal(document.Id, hit.DocumentId);
    }

    [Fact]
    public async Task Reimport_LeavesFullyIndexedDocumentUnchanged()
    {
        var title = "Idempotence-" + Guid.NewGuid().ToString("N");
        var corpus = OneDocument(title, "One indexed document");
        var invalidations = new CountingInvalidator();
        await using var context = _database.CreateContext();
        var importer = NewImporter(context, new DeterministicEmbeddingService(), invalidations);

        await importer.ImportAsync(corpus);
        var chunkId = await context.DocumentChunks.AsNoTracking()
            .Where(x => x.Document.Title == title)
            .Select(x => x.Id).SingleAsync();
        await importer.ImportAsync(corpus);

        Assert.Equal(chunkId, await context.DocumentChunks.AsNoTracking()
            .Where(x => x.Document.Title == title)
            .Select(x => x.Id).SingleAsync());
        Assert.Equal(1, invalidations.Count);
        Assert.Equal(1, await context.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => x.Chunk.Document.Title == title));
    }

    [Fact]
    public async Task Reimport_RepairsLegacyDocumentWithoutEmbeddingsAndPreservesContent()
    {
        var title = "Legacy-" + Guid.NewGuid().ToString("N");
        var document = new KnowledgeDocument(title, "Existing user edited content");
        document.AddChunk(0, 12, "Orphan chunk");
        var oldChunkId = Assert.Single(document.Chunks).Id;

        await using (var setup = _database.CreateContext())
        {
            await setup.Documents.AddAsync(document);
            await setup.SaveChangesAsync();
        }

        await using var context = _database.CreateContext();
        var invalidations = new CountingInvalidator();
        await NewImporter(context, new DeterministicEmbeddingService(), invalidations)
            .ImportAsync(OneDocument(title, "Old corpus content"));

        var stored = await context.Documents.AsNoTracking()
            .SingleAsync(x => x.Id == document.Id);
        Assert.Equal("Existing user edited content", stored.Content);
        Assert.False(await context.DocumentChunks.AnyAsync(x => x.Id == oldChunkId));
        var chunk = await context.DocumentChunks.AsNoTracking()
            .SingleAsync(x => x.DocumentId == stored.Id);
        Assert.Equal(stored.Content, chunk.Content);
        Assert.True(await context.ChunkEmbeddings.AsNoTracking()
            .AnyAsync(x => x.ChunkId == chunk.Id));
        Assert.Equal(1, invalidations.Count);
    }

    [Fact]
    public async Task EmbeddingFailure_IsAtomicPerDocumentAndCanResume()
    {
        var suffix = Guid.NewGuid().ToString("N");
        var firstTitle = "Good-" + suffix;
        var failedTitle = "Failure-" + suffix;
        var corpus = new CorpusRoot
        {
            Documents =
            [
                new CorpusDocument { Title = firstTitle, Content = "Valid content" },
                new CorpusDocument { Title = failedTitle, Content = new string('Q', 500) + "FAIL generation" }
            ]
        };

        await using var context = _database.CreateContext();
        var invalidations = new CountingInvalidator();
        var failing = NewImporter(context,
            new FailingEmbeddingService(), invalidations);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => failing.ImportAsync(corpus));

        Assert.True(await context.Documents.AsNoTracking()
            .AnyAsync(x => x.Title == firstTitle));
        Assert.False(await context.Documents.AsNoTracking()
            .AnyAsync(x => x.Title == failedTitle));
        Assert.Equal(1, invalidations.Count);

        await NewImporter(context, new DeterministicEmbeddingService(), invalidations)
            .ImportAsync(corpus);

        Assert.Equal(2, await context.Documents.AsNoTracking()
            .CountAsync(x => x.Title == firstTitle || x.Title == failedTitle));
        Assert.Equal(3, await context.ChunkEmbeddings.AsNoTracking()
            .CountAsync(x => x.Chunk.Document.Title == firstTitle ||
                x.Chunk.Document.Title == failedTitle));
        Assert.Equal(2, invalidations.Count);
    }

    private static CorpusRoot OneDocument(string title, string content) => new()
    {
        Documents = [new CorpusDocument { Title = title, Content = content }]
    };

    private static CorpusImporterService NewImporter(
        KnowledgeDbContext context,
        IEmbeddingService embeddings,
        IVectorIndexCacheInvalidator invalidator)
    {
        return new CorpusImporterService(
            context,
            new DefaultTextChunker(),
            new ChunkEmbeddingGenerator(embeddings),
            new KnowledgeDocumentRepository(context),
            invalidator);
    }

    private sealed class CountingInvalidator : IVectorIndexCacheInvalidator
    {
        public int Count { get; private set; }

        public Task InvalidateAsync()
        {
            Count++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmbeddingService : IEmbeddingService
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
}
