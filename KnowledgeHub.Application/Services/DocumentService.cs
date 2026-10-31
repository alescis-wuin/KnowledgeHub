using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KnowledgeHub.Application.Services;

public class DocumentService
    : IDocumentService
{
    private readonly IKnowledgeDocumentRepository _repository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly IKnowledgeSourceRepository _sourceRepository;
    private readonly ITagRepository _tagRepository;
    private readonly ITextChunker _chunker;
    private readonly IChunkEmbeddingGenerator _embeddingGenerator;
    private readonly IVectorIndexCacheInvalidator _cacheInvalidator;
    private readonly ILogger<DocumentService> _logger;

    private static readonly EventId CreatedEvent = new(52001, "DocumentCreated");
    private static readonly EventId ImportedEvent = new(52002, "DocumentImported");
    private static readonly EventId UpdatedEvent = new(52003, "DocumentUpdated");
    private static readonly EventId DeletedEvent = new(52004, "DocumentDeleted");

    public DocumentService(
        IKnowledgeDocumentRepository repository,
        ICategoryRepository categoryRepository,
        IKnowledgeSourceRepository sourceRepository,
        ITagRepository tagRepository,
        ITextChunker chunker,
        IChunkEmbeddingGenerator embeddingGenerator,
        IVectorIndexCacheInvalidator cacheInvalidator,
        ILogger<DocumentService>? logger = null)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _sourceRepository = sourceRepository;
        _tagRepository = tagRepository;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _cacheInvalidator = cacheInvalidator;
        _logger = logger ?? NullLogger<DocumentService>.Instance;
    }

    public async Task<List<DocumentDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var documents =
            await _repository.GetAllAsync(
                cancellationToken);

        return documents
            .Select(Map)
            .ToList();
    }

    public async Task<DocumentDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document =
            await _repository.GetByIdAsync(
                id,
                cancellationToken);

        if (document is null)
        {
            return null;
        }

        return Map(document);
    }

    public async Task<DocumentDto> CreateAsync(
    CreateDocumentDto dto,
    CancellationToken cancellationToken = default)
    {
        var document =
            new KnowledgeDocument(
                dto.Title,
                dto.Content);

        if (dto.ExternalId.HasValue)
        {
            document.AssignExternalId(dto.ExternalId.Value);
        }

        document.SetSummary(dto.Summary);
        document.SetSearchKeywords(dto.SearchKeywords);

        document.AssignCategory(
            dto.CategoryId);

        document.AssignSource(
            dto.SourceId);

        foreach (var tagId in dto.TagIds)
        {
            var tag =
                await _tagRepository.GetByIdAsync(
                    tagId,
                    cancellationToken);

            if (tag is not null)
            {
                document.AddTag(tag);
            }
        }

        var chunks = BuildChunks(
            document.Id,
            document.Content);

        var embeddings = await _embeddingGenerator.GenerateAsync(
            chunks,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        document.ReplaceChunks(chunks);

        await _repository.AddIndexedAsync(
            document,
            embeddings,
            cancellationToken);

        await _cacheInvalidator.InvalidateAsync();
        _logger.LogInformation(CreatedEvent,
            "Document {DocumentId} indexed with {ChunkCount} chunks",
            document.Id, chunks.Count);

        return Map(document);
    }

    /// <summary>
    /// Upsert by immutable external GUID. Does not re-embed metadata-only changes.
    /// The legacy title-based import remains in CorpusImporter for empty GUIDs.
    /// </summary>
    public async Task<ImportDocumentResultDto> ImportAsync(
        CreateDocumentDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.ExternalId is not Guid externalId || externalId == Guid.Empty)
        {
            throw new ArgumentException("A non-empty ExternalId is required.");
        }

        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Trim().Length > 300 ||
            string.IsNullOrWhiteSpace(dto.Content))
        {
            throw new ArgumentException("A valid title and non-empty content are required.");
        }

        var document = await _repository.GetByExternalIdAsync(
            externalId, cancellationToken);
        if (document is null)
        {
            var created = await CreateAsync(dto, cancellationToken);
            return new ImportDocumentResultDto(created, ImportDocumentOutcome.Created);
        }

        var desiredContent = dto.Content.Trim();
        var desiredHash = KnowledgeDocument.ComputeContentSha256(desiredContent);
        var existingHash = document.ContentSha256 ??
            KnowledgeDocument.ComputeContentSha256(document.Content);

        var contentChanged =
            !string.Equals(existingHash, desiredHash, StringComparison.Ordinal) ||
            !string.Equals(document.Content, desiredContent, StringComparison.Ordinal);

        var desiredTags = (dto.TagIds ?? []).ToHashSet();
        var existingTags = document.DocumentTags.Select(x => x.TagId).ToHashSet();

        var metadataChanged =
            !string.Equals(document.Title, dto.Title.Trim(), StringComparison.Ordinal) ||
            document.CategoryId != dto.CategoryId ||
            document.SourceId != dto.SourceId ||
            !string.Equals(document.Summary ?? string.Empty,
                dto.Summary ?? string.Empty, StringComparison.Ordinal) ||
            !string.Equals(document.SearchKeywords ?? string.Empty,
                dto.SearchKeywords ?? string.Empty, StringComparison.Ordinal) ||
            !existingTags.SetEquals(desiredTags);

        var indexComplete = await _repository.HasCompleteIndexAsync(
            document.Id, cancellationToken);

        if (!contentChanged && !metadataChanged && indexComplete)
        {
            _logger.LogDebug(ImportedEvent,
                "Document {DocumentId} import outcome {ImportOutcome}",
                document.Id, ImportDocumentOutcome.Unchanged);
            return new ImportDocumentResultDto(Map(document), ImportDocumentOutcome.Unchanged);
        }

        // Validate all requested tags before any tracked entity is modified.
        var newTags = new List<Tag>();
        foreach (var tagId in desiredTags.Except(existingTags))
        {
            var tag = await _tagRepository.GetByIdAsync(tagId, cancellationToken);
            if (tag is null)
            {
                throw new ArgumentException($"Unknown tag ID: {tagId}.");
            }

            newTags.Add(tag);
        }

        IReadOnlyList<DocumentChunk>? replacementChunks = null;
        IReadOnlyList<ChunkEmbedding>? replacementEmbeddings = null;
        if (contentChanged || !indexComplete)
        {
            // Ollama calls finish before touching the tracked document.
            replacementChunks = BuildChunks(document.Id, desiredContent);
            replacementEmbeddings = await _embeddingGenerator.GenerateAsync(
                replacementChunks, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (replacementChunks is not null)
        {
            document.Update(dto.Title, desiredContent);
        }

        document.UpdateMetadata(
            dto.Title, dto.CategoryId, dto.SourceId,
            dto.Summary, dto.SearchKeywords);

        foreach (var removed in document.DocumentTags
                     .Where(x => !desiredTags.Contains(x.TagId)).ToArray())
        {
            document.DocumentTags.Remove(removed);
        }

        foreach (var tag in newTags)
        {
            document.AddTag(tag);
        }

        if (replacementChunks is not null && replacementEmbeddings is not null)
        {
            document.ReplaceChunks(replacementChunks);
            await _repository.UpdateIndexedAsync(
                document, replacementEmbeddings, cancellationToken);
        }
        else
        {
            await _repository.UpdateAsync(document, cancellationToken);
        }

        await _cacheInvalidator.InvalidateAsync();

        var outcome = contentChanged ? ImportDocumentOutcome.UpdatedContent :
            !indexComplete ? ImportDocumentOutcome.RepairedIndex :
            ImportDocumentOutcome.UpdatedMetadata;

        _logger.LogInformation(ImportedEvent,
            "Document {DocumentId} import outcome {ImportOutcome}; Reindexed={Reindexed}",
            document.Id, outcome, replacementChunks is not null);
        return new ImportDocumentResultDto(Map(document), outcome);
    }

    public async Task<DocumentDto?> UpdateAsync(
        Guid id,
        UpdateDocumentDto dto,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var document = await _repository.GetByIdAsync(
            id,
            cancellationToken);

        if (document is null)
        {
            return null;
        }

        var title = dto.Title?.Trim() ?? string.Empty;
        var newContent = dto.Content?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (string.IsNullOrWhiteSpace(newContent))
        {
            throw new ArgumentException("Content is required.");
        }

        var contentChanged = !string.Equals(
            document.Content,
            newContent,
            StringComparison.Ordinal);

        if (!contentChanged)
        {
            document.Update(title, newContent);

            await _repository.UpdateAsync(
                document,
                cancellationToken);

            await _cacheInvalidator.InvalidateAsync();
            _logger.LogInformation(UpdatedEvent,
                "Document {DocumentId} updated; Reindexed={Reindexed}",
                document.Id, false);

            return Map(document);
        }

        // Prepare the complete replacement before changing
        // the tracked document or its existing chunks.
        var chunks = BuildChunks(
            document.Id,
            newContent);

        var embeddings = await _embeddingGenerator.GenerateAsync(
            chunks,
            cancellationToken);

        cancellationToken.ThrowIfCancellationRequested();

        // No external embedding calls remain after this point.
        document.Update(title, newContent);
        document.ReplaceChunks(chunks);

        await _repository.UpdateIndexedAsync(
            document,
            embeddings,
            cancellationToken);

        await _cacheInvalidator.InvalidateAsync();
        _logger.LogInformation(UpdatedEvent,
            "Document {DocumentId} updated; Reindexed={Reindexed}",
            document.Id, true);

        return Map(document);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(
            id,
            cancellationToken);

        await _cacheInvalidator.InvalidateAsync();
        _logger.LogInformation(DeletedEvent,
            "Document deletion requested for {DocumentId}", id);
    }

    public async Task<DocumentDetailsDto?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document =
            await _repository.GetByIdAsync(
                id,
                cancellationToken);

        if (document is null)
        {
            return null;
        }

        return new DocumentDetailsDto
        {
            Id = document.Id,

            Title = document.Title,

            Content = document.Content,

            CreatedAt = document.CreatedAt,

            Category = document.Category?.Name,

            Source = document.Source?.Name,

            Tags = document.DocumentTags
                .Select(x => x.Tag.Name)
                .ToList(),

            Chunks = document.Chunks
                .OrderBy(x => x.ChunkIndex)
                .Select(x => new ChunkDto
                {
                    Id = x.Id,

                    DocumentId = x.DocumentId,

                    ChunkIndex = x.ChunkIndex,

                    TokenCount = x.TokenCount,

                    Content = x.Content,

                    CreatedAt = x.CreatedAt
                })
                .ToList()
        };
    }

    public async Task<List<DocumentDto>> SearchAsync(
    string searchTerm,
    CancellationToken cancellationToken = default)
    {
        var documents =
            await _repository.SearchAsync(
                searchTerm,
                cancellationToken);

        return documents
            .Select(Map)
            .ToList();
    }

    public async Task<List<DocumentDto>> GetByCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken = default)
    {
        var documents =
            await _repository.GetByCategoryAsync(
                categoryId,
                cancellationToken);

        return documents
            .Select(Map)
            .ToList();
    }

    public async Task<List<DocumentDto>> GetBySourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default)
    {
        var documents =
            await _repository.GetBySourceAsync(
                sourceId,
                cancellationToken);

        return documents
            .Select(Map)
            .ToList();
    }

    public async Task<List<DocumentDto>> GetByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default)
    {
        var documents =
            await _repository.GetByTagAsync(
                tagId,
                cancellationToken);

        return documents
            .Select(Map)
            .ToList();
    }

    public async Task<KnowledgeStatisticsDto>
        GetStatisticsAsync(
        CancellationToken cancellationToken = default)
    {
        var statistics =
            await _repository.GetStatisticsAsync(
                cancellationToken);

        return new KnowledgeStatisticsDto
        {
            DocumentsCount =
                statistics.DocumentsCount,

            CategoriesCount =
                statistics.CategoriesCount,

            SourcesCount =
                statistics.SourcesCount,

            TagsCount =
                statistics.TagsCount,

            ChunksCount =
                statistics.ChunksCount
        };
    }

    private IReadOnlyList<DocumentChunk> BuildChunks(
        Guid documentId,
        string content)
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

        return texts
            .Select((text, index) => new DocumentChunk(
                documentId,
                index,
                text.Length,
                text))
            .ToArray();
    }

    private static DocumentDto Map(
        KnowledgeDocument document)
    {
        return new DocumentDto
        {
            Id = document.Id,
            ExternalId = document.ExternalId,
            ContentSha256 = document.ContentSha256,
            Title = document.Title,
            Content = document.Content,
            CreatedAt = document.CreatedAt,
            Tags = document.DocumentTags
                        .Select(x => x.Tag.Name)
                        .ToList()
        };
    }
}
