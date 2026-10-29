using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class VectorSearchService
    : IVectorSearchService
{
    private const string
        EmbeddingsCacheKey =
            EmbeddingCacheWarmupService
                .EmbeddingsCacheKey;

    private const string
        ChunksCacheKey =
            EmbeddingCacheWarmupService
                .ChunksCacheKey;

    private readonly KnowledgeDbContext
        _context;

    private readonly IEmbeddingService
        _embeddingService;

    private readonly IMemoryCache
        _memoryCache;

    public VectorSearchService(
        KnowledgeDbContext context,
        IEmbeddingService embeddingService,
        IMemoryCache memoryCache)
    {
        _context = context;
        _embeddingService =
            embeddingService;

        _memoryCache =
            memoryCache;
    }

    public async Task<
        IReadOnlyCollection<VectorSearchResultDto>>
        SearchAsync(
            string question,
            int topK = 5,
            CancellationToken cancellationToken =
                default)
    {
        if (string.IsNullOrWhiteSpace(question) || topK <= 0)
        {
            return [];
        }

        var embeddings = await GetEmbeddingsAsync(
            cancellationToken);

        if (embeddings.Count == 0)
        {
            return [];
        }

        var chunks = await GetChunksAsync(
            cancellationToken);

        if (chunks.Count == 0 ||
            !embeddings.Any(x => chunks.ContainsKey(x.ChunkId)))
        {
            return [];
        }

        var questionEmbedding =
            await _embeddingService
                .GenerateEmbeddingAsync(
                    question,
                    cancellationToken);

        var results =
    embeddings
        .Where(
            x =>
                chunks.ContainsKey(
                    x.ChunkId))
        .Select(
            x =>
            {
                var chunk =
                    chunks[
                        x.ChunkId];

                return new VectorSearchResultDto
                {
                    ChunkId =
                        chunk.Id,

                    DocumentId =
                        chunk.DocumentId,

                    Title =
                        chunk.Document.Title,

                    Content =
                        chunk.Content,

                    Score =
                        CosineSimilarity(
                            questionEmbedding,
                            x.Embedding)
                };
            })
        .OrderByDescending(
            x => x.Score)
        .GroupBy(
            x => x.DocumentId)
        .Select(
            x => x.First())
        .Take(topK)
        .ToList();

        return results;
    }

    private async Task<
        IReadOnlyCollection<ChunkEmbedding>>
        GetEmbeddingsAsync(
            CancellationToken cancellationToken)
    {
        if (
            _memoryCache.TryGetValue(
                EmbeddingsCacheKey,
                out IReadOnlyCollection<
                    ChunkEmbedding>? cached) &&
            cached is not null)
        {
            Console.WriteLine(
                "EMBEDDINGS FROM CACHE");

            return cached;
        }

        Console.WriteLine(
            "EMBEDDINGS FROM DATABASE");

        var embeddings =
            await _context
                .ChunkEmbeddings
                .AsNoTracking()
                .ToListAsync(
                    cancellationToken);

        _memoryCache.Set(
            EmbeddingsCacheKey,
            embeddings,
            TimeSpan.FromHours(12));

        return embeddings;
    }

    private async Task<
        IReadOnlyDictionary<
            Guid,
            DocumentChunk>>
        GetChunksAsync(
            CancellationToken cancellationToken)
    {
        if (
            _memoryCache.TryGetValue(
                ChunksCacheKey,
                out IReadOnlyDictionary<
                    Guid,
                    DocumentChunk>? cached) &&
            cached is not null)
        {
            Console.WriteLine(
                "CHUNKS FROM CACHE");

            return cached;
        }

        Console.WriteLine(
            "CHUNKS FROM DATABASE");

        var chunks =
            await _context
                .DocumentChunks
                .AsNoTracking()
                .Include(
                    x => x.Document)
                .ToListAsync(
                    cancellationToken);

        var dictionary =
            chunks.ToDictionary(
                x => x.Id);

        _memoryCache.Set(
            ChunksCacheKey,
            dictionary,
            TimeSpan.FromHours(12));

        return dictionary;
    }

    private static double
        CosineSimilarity(
            float[] vectorA,
            float[] vectorB)
    {
        if (
            vectorA.Length !=
            vectorB.Length)
        {
            return 0;
        }

        double dotProduct = 0;

        double normA = 0;

        double normB = 0;

        for (
            var i = 0;
            i < vectorA.Length;
            i++)
        {
            dotProduct +=
                vectorA[i] *
                vectorB[i];

            normA +=
                vectorA[i] *
                vectorA[i];

            normB +=
                vectorB[i] *
                vectorB[i];
        }

        if (
            normA == 0 ||
            normB == 0)
        {
            return 0;
        }

        return dotProduct /
               (
                   Math.Sqrt(normA) *
                   Math.Sqrt(normB)
               );
    }
}