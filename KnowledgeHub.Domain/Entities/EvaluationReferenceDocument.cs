namespace KnowledgeHub.Domain.Entities;

public class EvaluationReferenceDocument
{
    public Guid Id { get; private set; }

    public Guid EvaluationQuestionId { get; private set; }

    public EvaluationQuestion EvaluationQuestion { get; private set; }
        = null!;

    public string ExternalDocumentId { get; private set; } = string.Empty;

    public string? Title { get; private set; }

    public string? Content { get; private set; }

    private EvaluationReferenceDocument() { }

    public EvaluationReferenceDocument(
        Guid evaluationQuestionId,
        string externalDocumentId,
        string? title,
        string? content)
    {
        Id = Guid.NewGuid();
        EvaluationQuestionId = evaluationQuestionId;
        ExternalDocumentId = externalDocumentId;
        Title = title;
        Content = content;
    }
}