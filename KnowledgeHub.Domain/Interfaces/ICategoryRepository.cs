using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Domain.Interfaces;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}