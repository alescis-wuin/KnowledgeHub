using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface IKnowledgeSourceService
{
    Task<List<KnowledgeSourceDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<KnowledgeSourceDto> CreateAsync(
        CreateKnowledgeSourceDto dto,
        CancellationToken cancellationToken = default);
}