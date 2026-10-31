using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;

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

    public DocumentService(
        IKnowledgeDocumentRepository repository,
        ICategoryRepository categoryRepository,
        IKnowledgeSourceRepository sourceRepository,
        ITagRepository tagRepository,
        ITextChunker chunker,
        IChunkEmbeddingGenerator embeddingGenerator,
        IVectorIndexCacheInvalidator cacheInvalidator)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _sourceRepository = sourceRepository;
        _tagRepository = tagRepository;
        _chunker = chunker;
        _embeddingGenerator = embeddingGenerator;
        _cacheInvalidator = cacheInvalidator;
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

        return Map(document);
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
            Title = document.Title,
            Content = document.Content,
            CreatedAt = document.CreatedAt,
            Tags = document.DocumentTags
                        .Select(x => x.Tag.Name)
                        .ToList()
        };
    }
}
