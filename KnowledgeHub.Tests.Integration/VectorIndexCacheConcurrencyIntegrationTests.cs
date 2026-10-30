using System.Data.Common;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Infrastructure.Persistence.Context;
using KnowledgeHub.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;

namespace KnowledgeHub.Tests.Integration;

/// <summary>
/// Controls SQL-reader scheduling explicitly, without Thread.Sleep or Ollama.
/// Each class fixture uses an isolated PostgreSQL Testcontainers database.
/// </summary>
public sealed class VectorIndexCacheConcurrencyIntegrationTests
    : IClassFixture<IsolatedPostgresFixture>, IAsyncLifetime
{
    private static readonly TimeSpan TestDeadline = TimeSpan.FromSeconds(20);

    private readonly IsolatedPostgresFixture _database;
    private readonly DeterministicEmbeddingService _embeddings = new();

    public VectorIndexCacheConcurrencyIntegrationTests(
        IsolatedPostgresFixture database)
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
    public async Task ConcurrentColdSearches_LoadPostgresIndexOnlyOnce()
    {
        var documentId = await SeedAsync("Titre initial", "Contenu initial");
        var pause = new PauseFirstIndexReaderInterceptor();

        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new VectorIndexCache(memory);
        await using var firstContext = CreateInterceptedContext(pause);
        await using var secondContext = CreateInterceptedContext(pause);

        var firstSearch = new VectorSearchService(firstContext, _embeddings, cache);
        var secondSearch = new VectorSearchService(secondContext, _embeddings, cache);

        try
        {
            var first = firstSearch.SearchAsync("question");
            await pause.WaitUntilReaderOpenedAsync();

            // The second request cannot load its own snapshot while the
            // first one is still materializing the PostgreSQL result.
            var second = secondSearch.SearchAsync("question");
            Assert.False(second.IsCompleted);

            pause.Release();

            var firstHit = Assert.Single(await first.WaitAsync(TestDeadline));
            var secondHit = Assert.Single(await second.WaitAsync(TestDeadline));

            Assert.Equal(documentId, firstHit.DocumentId);
            Assert.Equal(firstHit.ChunkId, secondHit.ChunkId);
            Assert.Equal(firstHit.Content, secondHit.Content);
            Assert.Equal(1, pause.IndexQueryCount);
        }
        finally
        {
            pause.Release();
        }
    }

    [Fact]
    public async Task CancelledWaiter_DoesNotCancelOtherSearchOrPoisonCache()
    {
        var documentId = await SeedAsync("Titre", "Contenu initial");
        var pause = new PauseFirstIndexReaderInterceptor();

        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new VectorIndexCache(memory);
        await using var firstContext = CreateInterceptedContext(pause);
        await using var secondContext = CreateInterceptedContext(pause);

        var firstSearch = new VectorSearchService(firstContext, _embeddings, cache);
        var secondSearch = new VectorSearchService(secondContext, _embeddings, cache);

        try
        {
            var first = firstSearch.SearchAsync("question");
            await pause.WaitUntilReaderOpenedAsync();

            using var cancellation = new CancellationTokenSource();
            var cancelled = secondSearch.SearchAsync(
                "question", cancellationToken: cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await cancelled.WaitAsync(TestDeadline));

            pause.Release();
            var result = Assert.Single(await first.WaitAsync(TestDeadline));

            Assert.Equal(documentId, result.DocumentId);
            Assert.Equal(1, pause.IndexQueryCount);

            // A request after the cancellation still receives the hot index.
            var retry = Assert.Single(
                await secondSearch.SearchAsync("question").WaitAsync(TestDeadline));
            Assert.Equal(result.ChunkId, retry.ChunkId);
            Assert.Equal(1, pause.IndexQueryCount);
        }
        finally
        {
            pause.Release();
        }
    }

    [Fact]
    public async Task CommitDuringColdLoad_InvalidatePreventsStaleSnapshotReuse()
    {
        const string oldContent = "Version initiale";
        const string newContent = "Version indexee apres validation";

        var documentId = await SeedAsync("Titre ancien", oldContent);
        var pause = new PauseFirstIndexReaderInterceptor();

        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new VectorIndexCache(memory);
        await using var readerContext = CreateInterceptedContext(pause);
        var search = new VectorSearchService(readerContext, _embeddings, cache);

        try
        {
            // PostgreSQL has executed SELECT but its reader has not yet
            // been consumed. The command's statement snapshot is fixed.
            var inFlight = search.SearchAsync("question");
            await pause.WaitUntilReaderOpenedAsync();

            // Commit a new document/chunk/embedding on an independent DbContext.
            // An HTTP DocumentService write would invalidate immediately after
            // this commit; we schedule that same invalidation explicitly here.
            await ReplaceAsync(documentId, "Titre nouveau", newContent);
            var invalidation = cache.InvalidateAsync();
            Assert.False(invalidation.IsCompleted);

            pause.Release();

            var oldHit = Assert.Single(await inFlight.WaitAsync(TestDeadline));
            Assert.Equal(documentId, oldHit.DocumentId);
            Assert.Equal(oldContent, oldHit.Content);

            await invalidation.WaitAsync(TestDeadline);

            // Once invalidation completes, a later search MUST NOT return
            // the cached old reader's data, even if it finished after commit.
            var current = Assert.Single(
                await search.SearchAsync("question").WaitAsync(TestDeadline));
            Assert.Equal(documentId, current.DocumentId);
            Assert.Equal("Titre nouveau", current.Title);
            Assert.Equal(newContent, current.Content);
            Assert.NotEqual(oldHit.ChunkId, current.ChunkId);
            Assert.Equal(2, pause.IndexQueryCount);
        }
        finally
        {
            pause.Release();
        }
    }

    [Fact]
    public async Task CancelledColdLoad_ReleasesGateForNextSearch()
    {
        var documentId = await SeedAsync("Titre", "Contenu initial");
        var pause = new PauseFirstIndexReaderInterceptor();

        using var memory = new MemoryCache(new MemoryCacheOptions());
        using var cache = new VectorIndexCache(memory);
        await using var cancelledContext = CreateInterceptedContext(pause);
        await using var retryContext = CreateInterceptedContext(pause);

        var firstSearch = new VectorSearchService(
            cancelledContext, _embeddings, cache);
        var retrySearch = new VectorSearchService(
            retryContext, _embeddings, cache);

        try
        {
            using var cancellation = new CancellationTokenSource();
            var cancelled = firstSearch.SearchAsync(
                "question", cancellationToken: cancellation.Token);
            await pause.WaitUntilReaderOpenedAsync();
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                async () => await cancelled.WaitAsync(TestDeadline));

            pause.Release();
            var result = Assert.Single(
                await retrySearch.SearchAsync("question").WaitAsync(TestDeadline));

            Assert.Equal(documentId, result.DocumentId);
            Assert.Equal(2, pause.IndexQueryCount);
        }
        finally
        {
            pause.Release();
        }
    }

    private KnowledgeDbContext CreateInterceptedContext(DbCommandInterceptor interceptor)
    {
        var options = new DbContextOptionsBuilder<KnowledgeDbContext>()
            .UseNpgsql(_database.ConnectionString)
            .AddInterceptors(interceptor)
            .Options;

        return new KnowledgeDbContext(options);
    }

    private async Task<Guid> SeedAsync(string title, string content)
    {
        await using var context = _database.CreateContext();
        var document = new KnowledgeDocument(title, content);
        document.AddChunk(0, content.Length, content);

        var chunk = Assert.Single(document.Chunks);
        var vector = await _embeddings.GenerateEmbeddingAsync(chunk.Content);
        await new KnowledgeDocumentRepository(context).AddIndexedAsync(
            document, [new ChunkEmbedding(chunk.Id, vector)]);

        return document.Id;
    }

    private async Task ReplaceAsync(Guid id, string title, string content)
    {
        await using var context = _database.CreateContext();
        var repository = new KnowledgeDocumentRepository(context);
        var document = await repository.GetByIdAsync(id);
        Assert.NotNull(document);

        var replacement = new DocumentChunk(id, 0, content.Length, content);
        var vector = await _embeddings.GenerateEmbeddingAsync(replacement.Content);
        document.Update(title, content);
        document.ReplaceChunks([replacement]);

        await repository.UpdateIndexedAsync(
            document, [new ChunkEmbedding(replacement.Id, vector)]);
    }

    private sealed class PauseFirstIndexReaderInterceptor : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> _entered = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        private int _queryCount;

        public int IndexQueryCount => Volatile.Read(ref _queryCount);

        public Task WaitUntilReaderOpenedAsync() =>
            _entered.Task.WaitAsync(TestDeadline);

        public void Release() => _release.TrySetResult(true);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (!command.CommandText.Contains(
                    "FROM \"ChunkEmbeddings\"", StringComparison.Ordinal) ||
                !command.CommandText.Contains(
                    "INNER JOIN \"DocumentChunks\"", StringComparison.Ordinal))
            {
                return result;
            }

            if (Interlocked.Increment(ref _queryCount) == 1)
            {
                _entered.TrySetResult(true);
                await _release.Task.WaitAsync(cancellationToken);
            }

            return result;
        }
    }
}
