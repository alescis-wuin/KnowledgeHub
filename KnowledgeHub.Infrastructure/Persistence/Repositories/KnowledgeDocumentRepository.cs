using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Domain.Models;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Repositories;

public class KnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly KnowledgeDbContext _context;

    public KnowledgeDocumentRepository(KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        await _context.Documents.AddAsync(document, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
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
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    // Evaluates legacy index completeness without loading embedding arrays.
    public async Task<bool> HasCompleteIndexAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var chunkIds = await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => chunk.DocumentId == id)
            .Select(chunk => chunk.Id)
            .ToArrayAsync(cancellationToken);

        if (chunkIds.Length == 0)
        {
            return false;
        }

        var embeddedCount = await _context.ChunkEmbeddings
            .AsNoTracking()
            .CountAsync(embedding => chunkIds.Contains(embedding.ChunkId),
                cancellationToken);

        return embeddedCount == chunkIds.Length;
    }

    public async Task AddIndexedAsync(
        KnowledgeDocument document,
        IReadOnlyCollection<ChunkEmbedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        ValidateIndex(document, embeddings);

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Documents.AddAsync(document, cancellationToken);

        await _context.ChunkEmbeddings.AddRangeAsync(
            embeddings,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdateIndexedAsync(
        KnowledgeDocument document,
        IReadOnlyCollection<ChunkEmbedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        ValidateIndex(document, embeddings);

        if (_context.Entry(document).State == EntityState.Detached)
        {
            throw new InvalidOperationException(
                "Indexed updates require a tracked document.");
        }

        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancellationToken);

        // Detect removed and newly attached chunks.
        _context.ChangeTracker.DetectChanges();

        // Explicitly track replacement chunks if needed.
        foreach (var chunk in document.Chunks)
        {
            if (_context.Entry(chunk).State == EntityState.Detached)
            {
                _context.DocumentChunks.Add(chunk);
            }
        }

        // Persist document changes and chunk replacements first.
        // Old chunk embeddings are removed through FK cascade.
        await _context.SaveChangesAsync(cancellationToken);

        // All referenced chunks now exist inside the transaction.
        await _context.ChunkEmbeddings.AddRangeAsync(
            embeddings,
            cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        // Both operations become visible atomically.
        await transaction.CommitAsync(cancellationToken);
    }

    private static void ValidateIndex(
        KnowledgeDocument document,
        IReadOnlyCollection<ChunkEmbedding> embeddings)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(embeddings);

        var chunkIds = document.Chunks
            .Select(chunk => chunk.Id)
            .ToHashSet();

        if (chunkIds.Count == 0 ||
            chunkIds.Count != document.Chunks.Count ||
            chunkIds.Count != embeddings.Count ||
            !chunkIds.SetEquals(
                embeddings.Select(embedding => embedding.ChunkId)))
        {
            throw new InvalidOperationException(
                "Every chunk must have exactly one matching embedding.");
        }
    }

    public async Task UpdateAsync(
        KnowledgeDocument document,
        CancellationToken cancellationToken = default)
    {
        _context.Documents.Update(document);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await GetByIdAsync(id, cancellationToken);

        if (document is null)
        {
            return;
        }

        _context.Documents.Remove(document);
        await _context.SaveChangesAsync(cancellationToken);
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

    public async Task<List<KnowledgeDocument>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.CategoryId == categoryId)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>> GetBySourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Where(x => x.SourceId == sourceId)
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<KnowledgeDocument>> GetByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Documents
            .AsNoTracking()
            .Include(x => x.DocumentTags)
            .Where(x => x.DocumentTags.Any(t => t.TagId == tagId))
            .OrderBy(x => x.Title)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeStatistics> GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        return new KnowledgeStatistics
        {
            DocumentsCount =
                await _context.Documents.CountAsync(cancellationToken),
            CategoriesCount =
                await _context.Categories.CountAsync(cancellationToken),
            SourcesCount =
                await _context.Sources.CountAsync(cancellationToken),
            TagsCount =
                await _context.Tags.CountAsync(cancellationToken),
            ChunksCount =
                await _context.DocumentChunks.CountAsync(cancellationToken)
        };
    }
}
