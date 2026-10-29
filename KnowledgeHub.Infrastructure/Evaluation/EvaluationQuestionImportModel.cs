namespace KnowledgeHub.Application.Models.Evaluation;

public sealed class EvaluationQuestionImportModel
{
    public string ExternalQuestionId { get; init; } = string.Empty;

    public string Question { get; init; } = string.Empty;

    public string? ExpectedAnswer { get; init; }

    public string Difficulty { get; init; } = string.Empty;

    public List<EvaluationReferenceDocumentImportModel> Documents
    {
        get;
        init;
    } = [];
}