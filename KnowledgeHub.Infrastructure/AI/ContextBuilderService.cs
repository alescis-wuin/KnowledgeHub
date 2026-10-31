using KnowledgeHub.Application.Telemetry;
using System.Text;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class ContextBuilderService
    : IContextBuilderService
{
    private readonly IVectorSearchService
        _vectorSearchService;

    public ContextBuilderService(
        IVectorSearchService vectorSearchService)
    {
        _vectorSearchService =
            vectorSearchService;
    }

    public async Task<
        RetrievedContextDto>
        BuildAsync(
            string question,
            int topK = 5,
            CancellationToken cancellationToken =
                default)
    {
        var chunks = await KnowledgeHubActivities.TraceAsync<IReadOnlyCollection<VectorSearchResultDto>>(
            "rag.retrieve",
            () => _vectorSearchService.SearchAsync(question, topK, cancellationToken),
            cancellationToken);

        return KnowledgeHubActivities.Trace("rag.context", () =>
        {
        var sb =
            new StringBuilder();

        var index = 1;

        foreach (var chunk in chunks)
        {
            sb.AppendLine(
                $"SOURCE {index}");

            sb.AppendLine(
                $"PERTINENCE : {chunk.Score:P0}");

            sb.AppendLine(
                $"Title: {chunk.Title}");

            sb.AppendLine();

            sb.AppendLine(
                chunk.Content);

            sb.AppendLine();

            sb.AppendLine(
                "-----------------------------------");

            sb.AppendLine();

            index++;
        }

        return new RetrievedContextDto
        {
            Question = question,

            Context =
                sb.ToString(),

            Sources =
                chunks.ToList()
        };
        });
    }
}
