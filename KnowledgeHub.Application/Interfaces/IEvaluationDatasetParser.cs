using KnowledgeHub.Application.Models.Evaluation;

namespace KnowledgeHub.Application.Interfaces;

public interface IEvaluationDatasetParser
{
    string DatasetType { get; }

    Task<EvaluationDatasetImportModel> ParseAsync(
        string datasetPath,
        CancellationToken cancellationToken);
}