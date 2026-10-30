using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.CorpusImporter.Models;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.CorpusImporter.Services;

/// <summary>
/// Imports metadata and persists each document with its complete vector index.
/// An embedding failure leaves that document uncommitted; previously imported
/// documents remain available so a later run can resume safely.
/// </summary>
public sealed class CorpusImporter
{
    private readonly KnowledgeDbContext _context;
    private readonly ITextChunker _chunker;
    private readonly IChunkEmbeddingGenerator _embeddingGenerator;
    private readonly IKnowledgeDocumentRepository _documents;
    private readonly IVectorIndexCacheInvalidator _cacheInvalidator;

    public CorpusImporter(
        KnowledgeDbContext context,
        ITextChunker chunker,
        IChunkEmbeddingGenerator embeddingGenerator,
        IKnowledgeDocumentRepository documents,
        IVectorIndexCacheInvalidator cacheInvalidator)
    {
        _context = context;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _documents = documents;
        _cacheInvalidator = cacheInvalidator;
    }

    public async Task ImportAsync(
        CorpusRoot corpus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(corpus);

        await ImportMetadataAsync(corpus, cancellationToken);

        foreach (var item in corpus.Documents)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (string.IsNullOrWhiteSpace(item.Title) ||
                string.IsNullOrWhiteSpace(item.Content))
            {
                Console.WriteLine("Skipping document with empty title or content.");
                continue;
            }

            var title = item.Title.Trim();
            var existingId = await _context.Documents
                .AsNoTracking()
                .Where(document => document.Title == title)
                .Select(document => (Guid?)document.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingId is Guid id)
            {
                await RepairIncompleteIndexAsync(id, cancellationToken);
                _context.ChangeTracker.Clear();
                continue;
            }

            var document = new KnowledgeDocument(title, item.Content);
            document.SetSummary(item.Summary);
            document.SetSearchKeywords(item.SearchKeywords);

            var category = await _context.Categories
                .FirstOrDefaultAsync(
                    candidate => candidate.Name == item.CategoryName,
                    cancellationToken);
            if (category is not null)
            {
                document.AssignCategory(category.Id);
            }

            var source = await _context.Sources
                .FirstOrDefaultAsync(
                    candidate => candidate.Name == item.SourceName,
                    cancellationToken);
            if (source is not null)
            {
                document.AssignSource(source.Id);
            }

            foreach (var tagName in item.TagNames.Distinct(StringComparer.Ordinal))
            {
                var tag = await _context.Tags
                    .FirstOrDefaultAsync(
                        candidate => candidate.Name == tagName,
                        cancellationToken);
                if (tag is not null)
                {
                    document.AddTag(tag);
                }
            }

            var chunks = BuildChunks(document.Id, document.Content);
            var embeddings = await _embeddingGenerator.GenerateAsync(
                chunks, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            document.ReplaceChunks(chunks);
            await _documents.AddIndexedAsync(
                document, embeddings, cancellationToken);

            // The commit has completed. Invalidation must not be cancelled
            // simply because the caller disconnected afterwards.
            await _cacheInvalidator.InvalidateAsync();
            _context.ChangeTracker.Clear();
        }
    }

    private async Task ImportMetadataAsync(
        CorpusRoot corpus,
        CancellationToken cancellationToken)
    {
        foreach (var name in corpus.Categories
            .Select(category => category.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal))
        {
            if (!await _context.Categories
                .AnyAsync(category => category.Name == name, cancellationToken))
            {
                _context.Categories.Add(new Category(name));
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var source in corpus.Sources
            .Where(source => !string.IsNullOrWhiteSpace(source.Name))
            .DistinctBy(source => source.Name.Trim(), StringComparer.Ordinal))
        {
            var name = source.Name.Trim();
            if (!await _context.Sources
                .AnyAsync(candidate => candidate.Name == name, cancellationToken))
            {
                _context.Sources.Add(new KnowledgeSource(name, source.Url));
            }
        }
        await _context.SaveChangesAsync(cancellationToken);

        foreach (var name in corpus.Tags
            .Select(tag => tag.Name?.Trim())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal))
        {
            if (!await _context.Tags
                .AnyAsync(tag => tag.Name == name, cancellationToken))
            {
                _context.Tags.Add(new Tag(name));
            }
        }
        await _context.SaveChangesAsync(cancellationToken);
        _context.ChangeTracker.Clear();
    }

    private async Task RepairIncompleteIndexAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var document = await _documents.GetByIdAsync(id, cancellationToken)
            ?? throw new InvalidOperationException(
                "Document disappeared while checking its index.");

        var embeddedChunkIds = await _context.ChunkEmbeddings
            .AsNoTracking()
            .Where(embedding => embedding.Chunk.DocumentId == id)
            .Select(embedding => embedding.ChunkId)
            .ToArrayAsync(cancellationToken);

        var ids = embeddedChunkIds.ToHashSet();
        if (document.Chunks.Count > 0 &&
            document.Chunks.Count == ids.Count &&
            document.Chunks.All(chunk => ids.Contains(chunk.Id)))
        {
            return;
        }

        // Preserve the existing document and its metadata rather than
        // overwriting user edits with potentially older corpus content.
        var replacements = BuildChunks(document.Id, document.Content);
        var embeddings = await _embeddingGenerator.GenerateAsync(
            replacements, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        document.ReplaceChunks(replacements);
        await _documents.UpdateIndexedAsync(
            document, embeddings, cancellationToken);
        await _cacheInvalidator.InvalidateAsync();
    }

    private IReadOnlyList<DocumentChunk> BuildChunks(Guid id, string content)
    {
        var texts = _chunker.Chunk(content)
            .Select(chunk => chunk?.Trim())
            .Where(chunk => !string.IsNullOrWhiteSpace(chunk))
            .Select(chunk => chunk!)
            .ToArray();

        if (texts.Length == 0)
        {
            throw new InvalidOperationException(
                "Chunking produced no usable chunks.");
        }

        return texts.Select((text, index) => new DocumentChunk(
                id, index, text.Length, text))
            .ToArray();
    }
}
