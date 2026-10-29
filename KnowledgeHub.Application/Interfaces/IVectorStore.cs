namespace KnowledgeHub.Application.Interfaces;

public interface IVectorStore
{
    Task StoreAsync(
        Guid chunkId,
        float[] embedding,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<Guid>> SearchAsync(
        float[] embedding,
        int topK = 5,
        CancellationToken cancellationToken = default);
}