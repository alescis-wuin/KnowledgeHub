using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;

namespace KnowledgeHub.Application.Services;

public class TagService : ITagService
{
    private readonly ITagRepository _repository;

    public TagService(
        ITagRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<TagDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var tags =
            await _repository.GetAllAsync(
                cancellationToken);

        return tags
            .Select(x => new TagDto
            {
                Id = x.Id,
                Name = x.Name
            })
            .ToList();
    }

    public async Task<TagDto> CreateAsync(
        CreateTagDto dto,
        CancellationToken cancellationToken = default)
    {
        var tag =
            new Tag(dto.Name);

        await _repository.AddAsync(
            tag,
            cancellationToken);

        return new TagDto
        {
            Id = tag.Id,
            Name = tag.Name
        };
    }
}