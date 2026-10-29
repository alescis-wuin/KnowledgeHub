namespace KnowledgeHub.Application.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);
}