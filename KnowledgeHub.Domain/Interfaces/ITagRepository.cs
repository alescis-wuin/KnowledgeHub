using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Domain.Interfaces;

public interface ITagRepository
{
    Task<List<Tag>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Tag?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Tag tag,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}