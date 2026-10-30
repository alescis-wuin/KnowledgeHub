using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KnowledgeHub.Infrastructure.AI;

// Store the data required by vector retrieval in one coherent cache entry.
// Projection avoids repeatedly loading the full document content per chunk.
internal sealed record VectorIndexEntry(
    Guid ChunkId,
    Guid DocumentId,
    string Title,
    string Content,
    float[] Embedding);

public sealed class VectorIndexCache : IVectorIndexCacheInvalidator, IDisposable
{
    private const string CacheKey = "knowledgehub_vector_index_snapshot";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(12);

    private readonly IMemoryCache _cache;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public VectorIndexCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    // A single SQL query produces a consistent PostgreSQL statement snapshot.
    // The gate serializes cold loads and invalidations, preventing an older
    // in-flight load from repopulating the cache after invalidation finishes.
    internal async Task<IReadOnlyList<VectorIndexEntry>> GetAsync(
        KnowledgeDbContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(
                    CacheKey,
                    out IReadOnlyList<VectorIndexEntry>? cached) &&
                cached is not null)
            {
                return cached;
            }

            var entries = await context.ChunkEmbeddings
                .AsNoTracking()
                .Select(embedding => new VectorIndexEntry(
                    embedding.ChunkId,
                    embedding.Chunk.DocumentId,
                    embedding.Chunk.Document.Title,
                    embedding.Chunk.Content,
                    embedding.Embedding))
                .ToArrayAsync(cancellationToken);

            _cache.Set(CacheKey, entries, Lifetime);
            return entries;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task InvalidateAsync()
    {
        await _gate.WaitAsync(CancellationToken.None);
        try
        {
            _cache.Remove(CacheKey);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }
}
