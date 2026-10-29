using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Repositories;

public class KnowledgeSourceRepository
    : IKnowledgeSourceRepository
{
    private readonly KnowledgeDbContext _context;

    public KnowledgeSourceRepository(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task<List<KnowledgeSource>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeSource?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Sources
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task AddAsync(
        KnowledgeSource source,
        CancellationToken cancellationToken = default)
    {
        await _context.Sources.AddAsync(
            source,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var source =
            await GetByIdAsync(
                id,
                cancellationToken);

        if (source is null)
        {
            return;
        }

        _context.Sources.Remove(source);

        await _context.SaveChangesAsync(
            cancellationToken);
    }
}