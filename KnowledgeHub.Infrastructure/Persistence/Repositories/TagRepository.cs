using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Repositories;

public class TagRepository
    : ITagRepository
{
    private readonly KnowledgeDbContext _context;

    public TagRepository(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Tag>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<Tag?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Tags
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        Tag tag,
        CancellationToken cancellationToken = default)
    {
        await _context.Tags.AddAsync(
            tag,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tag =
            await GetByIdAsync(
                id,
                cancellationToken);

        if (tag is null)
        {
            return;
        }

        _context.Tags.Remove(tag);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}