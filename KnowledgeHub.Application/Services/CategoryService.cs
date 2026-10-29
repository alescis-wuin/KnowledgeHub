using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;

namespace KnowledgeHub.Application.Services;

public class CategoryService
    : ICategoryService
{
    private readonly ICategoryRepository _repository;

    public CategoryService(
        ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<CategoryDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var categories =
            await _repository.GetAllAsync(
                cancellationToken);

        return categories
            .Select(x => new CategoryDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToList();
    }

    public async Task<CategoryDto> CreateAsync(
        CreateCategoryDto dto,
        CancellationToken cancellationToken = default)
    {
        var category =
            new Category(dto.Name);

        await _repository.AddAsync(
            category,
            cancellationToken);

        return new CategoryDto
        {
            Id = category.Id,
            Name = category.Name
        };
    }
}