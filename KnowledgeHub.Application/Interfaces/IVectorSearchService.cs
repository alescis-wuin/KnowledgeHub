using KnowledgeHub.Application.DTOs;

namespace KnowledgeHub.Application.Interfaces;

public interface IVectorSearchService
{
    Task<IReadOnlyCollection<VectorSearchResultDto>>
        SearchAsync(
            string question,
            int topK = 5,
            CancellationToken cancellationToken =
                default);
}