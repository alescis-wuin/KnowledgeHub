using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.Interfaces;

namespace KnowledgeHub.Application.Services;

public class KnowledgeSourceService
    : IKnowledgeSourceService
{
    private readonly IKnowledgeSourceRepository _repository;

    public KnowledgeSourceService(
        IKnowledgeSourceRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<KnowledgeSourceDto>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var sources =
            await _repository.GetAllAsync(
                cancellationToken);

        return sources
            .Select(x => new KnowledgeSourceDto
            {
                Id = x.Id,
                Name = x.Name,
                Url = x.Url
            })
            .ToList();
    }

    public async Task<KnowledgeSourceDto> CreateAsync(
        CreateKnowledgeSourceDto dto,
        CancellationToken cancellationToken = default)
    {
        var source =
            new KnowledgeSource(
                dto.Name,
                dto.Url);

        await _repository.AddAsync(
            source,
            cancellationToken);

        return new KnowledgeSourceDto
        {
            Id = source.Id,
            Name = source.Name,
            Url = source.Url
        };
    }
}