using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface ICategoryService
{
    Task<List<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<CategoryDto> CreateAsync(
        CreateCategoryDto dto,
        CancellationToken cancellationToken = default);
}