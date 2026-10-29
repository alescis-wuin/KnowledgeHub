using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Domain.Models;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Repositories;

public class KnowledgeDocumentRepository
    : IKnowledgeDocumentRepository
{
    private readonly KnowledgeDbContext _context;

    public KnowledgeDocumentRepository(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        await _context.Documents.AddAsync(
            document,
            cancellationToken);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<List<KnowledgeDocument>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsSplitQuery()
            .Include(x => x.Category)
            .Include(x => x.Source)
            .Include(x => x.Chunks)
            .Include(x => x.DocumentTags)
                .ThenInclude(x => x.Tag)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeDocument?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsSplitQuery()
            .Include(x => x.Category)
            .Include(x => x.Source)
            .Include(x => x.Chunks)
            .Include(x => x.DocumentTags)
                .ThenInclude(x => x.Tag)
            .FirstOrDefaultAsync(
                x => x.Id == id,
                cancellationToken);
    }

    public async Task UpdateAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        _context.Documents.Update(document);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document =
            await GetByIdAsync(
                id,
                cancellationToken);

        if (document is null)
        {
            return;
        }

        _context.Documents.Remove(document);

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<List<KnowledgeDocument>> SearchAsync(
    string searchTerm,
    CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x =>
                x.Title.Contains(searchTerm) ||
                x.Content.Contains(searchTerm))
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>>
    GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.CategoryId == categoryId)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>>
    GetBySourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.SourceId == sourceId)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>>
    GetByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Include(x => x.DocumentTags)
            .Where(x =>
                x.DocumentTags.Any(
                    t => t.TagId == tagId))
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeStatistics> GetStatisticsAsync(
    CancellationToken cancellationToken = default)
    {
        return new KnowledgeStatistics
        {
            DocumentsCount =
                await _context.Documents.CountAsync(
                    cancellationToken),

            CategoriesCount =
                await _context.Categories.CountAsync(
                    cancellationToken),

            SourcesCount =
                await _context.Sources.CountAsync(
                    cancellationToken),

            TagsCount =
                await _context.Tags.CountAsync(
                    cancellationToken),

            ChunksCount =
                await _context.DocumentChunks.CountAsync(
                    cancellationToken)
        };
    }
}