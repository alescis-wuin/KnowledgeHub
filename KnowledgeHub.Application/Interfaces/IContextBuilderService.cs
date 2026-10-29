using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface IContextBuilderService
{
    Task<RetrievedContextDto>
        BuildAsync(
            string question,
            int topK = 5,
            CancellationToken cancellationToken =
                default);
}