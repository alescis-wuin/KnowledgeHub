using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface ITagService
{
    Task<List<TagDto>> GetAllAsync(
        CancellationToken cancellationToken = default);

    Task<TagDto> CreateAsync(
        CreateTagDto dto,
        CancellationToken cancellationToken = default);
}