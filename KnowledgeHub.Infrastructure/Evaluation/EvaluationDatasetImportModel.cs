namespace KnowledgeHub.Application.Models.Evaluation;

public sealed class EvaluationDatasetImportModel
{
    public string DatasetName { get; init; } = string.Empty;

    public string SourceType { get; init; } = string.Empty;

    public List<EvaluationQuestionImportModel> Questions { get; init; }
        = [];
}