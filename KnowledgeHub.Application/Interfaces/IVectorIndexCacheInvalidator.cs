namespace KnowledgeHub.Application.Interfaces;

// Application port: writing a document invalidates the local vector index.
public interface IVectorIndexCacheInvalidator
{
    // Intentionally has no request CancellationToken: after persistence succeeds,
    // invalidation must still run even if the HTTP request was cancelled.
    Task InvalidateAsync();
}
