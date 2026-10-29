namespace KnowledgeHub.Application.Models.Evaluation;

public sealed class EvaluationReferenceDocumentImportModel
{
    public string ExternalDocumentId { get; init; } = string.Empty;

    public string? Title { get; init; }

    public string? Content { get; init; }
}