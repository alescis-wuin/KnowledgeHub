namespace KnowledgeHub.Domain.Entities;

public class EvaluationQuestion
{
    public Guid Id { get; private set; }

    public Guid DatasetId { get; private set; }

    public EvaluationDataset Dataset { get; private set; } = null!;

    public string Question { get; private set; } = string.Empty;

    public string? ExpectedAnswer { get; private set; }

    public string Difficulty { get; private set; } = string.Empty;

    public string ExternalQuestionId { get; private set; } = string.Empty;

    public ICollection<EvaluationReferenceDocument> ReferenceDocuments { get; private set; }
        = new List<EvaluationReferenceDocument>();

    private EvaluationQuestion() { }

    public EvaluationQuestion(
        Guid datasetId,
        string externalQuestionId,
        string question,
        string? expectedAnswer,
        string difficulty)
    {
        Id = Guid.NewGuid();
        DatasetId = datasetId;
        ExternalQuestionId = externalQuestionId;
        Question = question;
        ExpectedAnswer = expectedAnswer;
        Difficulty = difficulty;
    }
}