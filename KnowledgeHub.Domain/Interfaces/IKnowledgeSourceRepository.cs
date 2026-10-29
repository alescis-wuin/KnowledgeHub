using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Domain.Interfaces;

public interface IKnowledgeSourceRepository
{
    Task<List<KnowledgeSource>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<KnowledgeSource?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        KnowledgeSource source,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}