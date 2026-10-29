using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Models;

namespace KnowledgeHub.Domain.Interfaces;

public interface IKnowledgeDocumentRepository
{
    Task<List<KnowledgeDocument>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<KnowledgeDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocument>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocument>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocument>> GetBySourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<List<KnowledgeDocument>> GetByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default);

    Task<KnowledgeStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default);

	Task AddIndexedAsync(
	    KnowledgeDocument document,
	    IReadOnlyCollection<ChunkEmbedding> embeddings,
	    CancellationToken cancellationToken = default);

	Task UpdateIndexedAsync(
	    KnowledgeDocument document,
	    IReadOnlyCollection<ChunkEmbedding> embeddings,
	    CancellationToken cancellationToken = default);
}
