using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.CorpusImporter.Services;

public class EmbeddingGenerationService
{
    private readonly KnowledgeDbContext _context;

    private readonly IEmbeddingService _embeddingService;

    private readonly IVectorStore _vectorStore;

    public EmbeddingGenerationService(
        KnowledgeDbContext context,
        IEmbeddingService embeddingService,
        IVectorStore vectorStore)
    {
        _context = context;
        _embeddingService = embeddingService;
        _vectorStore = vectorStore;
    }

    public async Task GenerateAsync(
        CancellationToken cancellationToken = default)
    {
        Console.WriteLine(
            "Recherche des chunks à vectoriser...");

        var alreadyEmbeddedChunkIds =
            await _context.ChunkEmbeddings
                .AsNoTracking()
                .Select(x => x.ChunkId)
                .ToListAsync(cancellationToken);

        var chunks =
            await _context.DocumentChunks
                .AsNoTracking()
                .Where(x =>
                    !alreadyEmbeddedChunkIds.Contains(
                        x.Id))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync(cancellationToken);

        Console.WriteLine(
            $"Chunks à vectoriser : {chunks.Count}");

        var processed = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var embedding =
                    await _embeddingService
                        .GenerateEmbeddingAsync(
                            chunk.Content,
                            cancellationToken);

                await _vectorStore.StoreAsync(
                    chunk.Id,
                    embedding,
                    cancellationToken);

                processed++;

                if (processed % 10 == 0)
                {
                    Console.WriteLine(
                        $"Progression : {processed}/{chunks.Count}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"Erreur chunk {chunk.Id}");

                Console.WriteLine(
                    ex.Message);
            }
        }

        Console.WriteLine(
            $"Vectorisation terminée : {processed} chunk(s)");
    }
}