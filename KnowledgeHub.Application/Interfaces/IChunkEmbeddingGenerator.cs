using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Application.Interfaces;

public interface IChunkEmbeddingGenerator
{
    Task<IReadOnlyList<ChunkEmbedding>> GenerateAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);
}
