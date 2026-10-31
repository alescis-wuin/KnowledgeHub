using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface IDocumentService
{
    Task<List<DocumentDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<DocumentDto?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DocumentDetailsDto?> GetDetailsAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<DocumentDto> CreateAsync(
        CreateDocumentDto dto,
        CancellationToken cancellationToken = default);

    Task<ImportDocumentResultDto> ImportAsync(
        CreateDocumentDto dto,
        CancellationToken cancellationToken = default);

    Task<DocumentDto?> UpdateAsync(
        Guid id,
        UpdateDocumentDto dto,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<List<DocumentDto>> SearchAsync(
        string searchTerm,
        CancellationToken cancellationToken = default);

    Task<List<DocumentDto>> GetByCategoryAsync(
    Guid categoryId,
    CancellationToken cancellationToken = default);

    Task<List<DocumentDto>> GetBySourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<List<DocumentDto>> GetByTagAsync(
        Guid tagId,
        CancellationToken cancellationToken = default);

    Task<KnowledgeStatisticsDto> GetStatisticsAsync(
        CancellationToken cancellationToken = default);
}