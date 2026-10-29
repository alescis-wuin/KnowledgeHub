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

    public DocumentService(
        IKnowledgeDocumentRepository repository,
        ICategoryRepository categoryRepository,
        IKnowledgeSourceRepository sourceRepository,
        ITagRepository tagRepository,
        ITextChunker chunker)
    {
        _repository = repository;
        _categoryRepository = categoryRepository;
        _sourceRepository = sourceRepository;
        _tagRepository = tagRepository;
        _chunker = chunker;
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

        var chunks =
            _chunker.Chunk(
                dto.Content);

        var index = 0;

        foreach (var chunk in chunks)
        {
            document.AddChunk(
                index++,
                chunk.Length,
                chunk);
        }

        await _repository.AddAsync(
            document,
            cancellationToken);

        return Map(document);
    }

    public async Task<DocumentDto?> UpdateAsync(
        Guid id,
        UpdateDocumentDto dto,
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

        document.Update(
            dto.Title,
            dto.Content);

        await _repository.UpdateAsync(
            document,
            cancellationToken);

        return Map(document);
    }

    public async Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(
            id,
            cancellationToken);
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