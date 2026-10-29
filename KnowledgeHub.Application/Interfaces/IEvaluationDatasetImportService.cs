namespace KnowledgeHub.Application.Interfaces;

public interface IEvaluationDatasetImportService
{
    Task ImportAllAsync(
        CancellationToken cancellationToken);
}