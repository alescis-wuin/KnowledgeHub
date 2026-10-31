using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class VectorSearchService : IVectorSearchService
{
    private readonly KnowledgeDbContext _context;
    private readonly IEmbeddingService _embeddingService;
    private readonly VectorIndexCache _indexCache;

    public VectorSearchService(
        KnowledgeDbContext context,
        IEmbeddingService embeddingService,
        VectorIndexCache indexCache)
    {
        _context = context;
        _embeddingService = embeddingService;
        _indexCache = indexCache;
    }

    public async Task<IReadOnlyCollection<VectorSearchResultDto>> SearchAsync(
        string question,
        int topK = 5,
        CancellationToken cancellationToken = default)
    {
        return await KnowledgeHubMetrics.MeasureRetrievalAsync(() =>
            KnowledgeHubActivities.TraceAsync<IReadOnlyCollection<VectorSearchResultDto>>(
            "vector_search.search", async () =>
        {
        System.Diagnostics.Activity.Current?.SetTag("kh.retrieval.top_k", topK);
        if (string.IsNullOrWhiteSpace(question) || topK <= 0)
        {
            return [];
        }

        var entries = await _indexCache.GetAsync(
            _context,
            cancellationToken);

        if (entries.Count == 0)
        {
            return [];
        }

        var questionEmbedding = await _embeddingService
            .GenerateEmbeddingAsync(question, cancellationToken);

        var ranked = KnowledgeHubActivities.Trace("vector_search.rank", () =>
            entries
            .Select(entry => new VectorSearchResultDto
            {
                ChunkId = entry.ChunkId,
                DocumentId = entry.DocumentId,
                Title = entry.Title,
                Content = entry.Content,
                Score = CosineSimilarity(questionEmbedding, entry.Embedding)
            })
            .OrderByDescending(result => result.Score)
            .GroupBy(result => result.DocumentId)
            .Select(group => group.First())
            .Take(topK)
            .ToList());
        System.Diagnostics.Activity.Current?.SetTag("kh.retrieval.result_count", ranked.Count);
        return ranked;
        }, cancellationToken), cancellationToken);
    }

    private static double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA.Length != vectorB.Length)
        {
            return 0;
        }

        double dotProduct = 0;
        double normA = 0;
        double normB = 0;

        for (var i = 0; i < vectorA.Length; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA == 0 || normB == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
