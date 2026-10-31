using KnowledgeHub.Application.Telemetry;

using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Application.Services;

public sealed class ChunkEmbeddingGenerator
    : IChunkEmbeddingGenerator
{
    private readonly IEmbeddingService _embeddingService;

    public ChunkEmbeddingGenerator(
        IEmbeddingService embeddingService)
    {
        _embeddingService = embeddingService;
    }

    public async Task<IReadOnlyList<ChunkEmbedding>> GenerateAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        return await KnowledgeHubActivities.TraceAsync("document.embed_batch", async () =>
        {
        ArgumentNullException.ThrowIfNull(chunks);

        if (chunks.Count == 0)
        {
            throw new ArgumentException(
                "At least one chunk is required.",
                nameof(chunks));
        }

        System.Diagnostics.Activity.Current?.SetTag("kh.embedding.chunk_count", chunks.Count);
        var orderedChunks = chunks
            .OrderBy(chunk => chunk.ChunkIndex)
            .ToArray();

        var uniqueChunkIds = orderedChunks
            .Select(chunk => chunk.Id)
            .Distinct()
            .Count();

        if (uniqueChunkIds != orderedChunks.Length)
        {
            throw new ArgumentException(
                "Duplicate chunk identifiers are not allowed.",
                nameof(chunks));
        }

        var results = new List<ChunkEmbedding>(
            orderedChunks.Length);

        int? expectedDimensions = null;

        foreach (var chunk in orderedChunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var vector = await _embeddingService
                .GenerateEmbeddingAsync(
                    chunk.Content,
                    cancellationToken);

            cancellationToken.ThrowIfCancellationRequested();

            if (vector is null ||
                vector.Length == 0 ||
                vector.Any(value => !float.IsFinite(value)) ||
                vector.All(value => value == 0f))
            {
                throw new InvalidOperationException(
                    $"Invalid embedding for chunk {chunk.Id}.");
            }

            if (expectedDimensions is int dimensions &&
                vector.Length != dimensions)
            {
                throw new InvalidOperationException(
                    $"Embedding dimensions mismatch for chunk {chunk.Id}. " +
                    $"Expected {dimensions}, received {vector.Length}.");
            }

            expectedDimensions ??= vector.Length;

            results.Add(new ChunkEmbedding(
                chunk.Id,
                vector.ToArray()));
        }

        System.Diagnostics.Activity.Current?.SetTag("kh.embedding.dimension", expectedDimensions ?? 0);
        return results;
        }, cancellationToken);
    }
}
