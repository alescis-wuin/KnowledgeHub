using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.AI;

public class PgVectorStore
    : IVectorStore
{
    private readonly KnowledgeDbContext _context;

    public PgVectorStore(
        KnowledgeDbContext context)
    {
        _context = context;
    }

    public async Task StoreAsync(
        Guid chunkId,
        float[] embedding,
        CancellationToken cancellationToken = default)
    {
        var exists =
            await _context.ChunkEmbeddings
                .AnyAsync(
                    x => x.ChunkId == chunkId,
                    cancellationToken);

        if (exists)
        {
            return;
        }

        _context.ChunkEmbeddings.Add(
            new ChunkEmbedding(
                chunkId,
                embedding));

        await _context.SaveChangesAsync(
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<Guid>>
        SearchAsync(
            float[] embedding,
            int topK = 5,
            CancellationToken cancellationToken = default)
    {
        return await _context
            .ChunkEmbeddings
            .AsNoTracking()
            .Take(topK)
            .Select(x => x.ChunkId)
            .ToListAsync(cancellationToken);
    }
}