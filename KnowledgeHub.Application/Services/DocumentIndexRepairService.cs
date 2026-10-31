using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KnowledgeHub.Application.Services;

/// <summary>
/// Repairs an incomplete legacy index through the same transactional repository
/// and embedding generator used by regular document writes.
/// </summary>
public sealed class DocumentIndexRepairService
{
    private readonly IKnowledgeDocumentRepository _documents;
    private readonly ITextChunker _chunker;
    private readonly IChunkEmbeddingGenerator _embeddings;
    private readonly IVectorIndexCacheInvalidator _cache;
    private readonly ILogger<DocumentIndexRepairService> _logger;
    private static readonly EventId RepairedEvent = new(52005, "DocumentIndexRepaired");

    public DocumentIndexRepairService(
        IKnowledgeDocumentRepository documents,
        ITextChunker chunker,
        IChunkEmbeddingGenerator embeddings,
        IVectorIndexCacheInvalidator cache,
        ILogger<DocumentIndexRepairService>? logger = null)
    {
        _documents = documents;
        _chunker = chunker;
        _embeddings = embeddings;
        _cache = cache;
        _logger = logger ?? NullLogger<DocumentIndexRepairService>.Instance;
    }

    /// <returns>Null if missing, false if complete, true if repaired.</returns>
    public async Task<bool?> RepairIfIncompleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await _documents.GetByIdAsync(id, cancellationToken);
        if (document is null)
        {
            return null;
        }

        if (await _documents.HasCompleteIndexAsync(id, cancellationToken))
        {
            return false;
        }

        var texts = _chunker.Chunk(document.Content)
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .ToArray();

        if (texts.Length == 0)
        {
            throw new InvalidOperationException("Chunking produced no usable chunks.");
        }

        var replacements = texts
            .Select((text, index) => new DocumentChunk(
                document.Id, index, text.Length, text))
            .ToArray();

        var embeddings = await _embeddings.GenerateAsync(
            replacements, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Bump the concurrency token while preserving existing user metadata.
        document.Update(document.Title, document.Content);
        document.ReplaceChunks(replacements);

        await _documents.UpdateIndexedAsync(
            document, embeddings, cancellationToken);
        await _cache.InvalidateAsync();
        _logger.LogInformation(RepairedEvent,
            "Document {DocumentId} index repaired with {ChunkCount} chunks",
            document.Id, replacements.Length);
        return true;
    }
}
