using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Repositories;

public class CategoryRepository
    : ICategoryRepository
{
    private readonly KnowledgeDbContext _context;

    public CategoryRepository(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Category>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Category?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        Category category,
        CancellationToken cancellationToken = default)
    {
        await _context.Categories.AddAsync(
            category,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var category =
            await GetByIdAsync(
                id,
                cancellationToken);

        if (category is null)
        {
            return;
        }

        _context.Categories.Remove(category);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}