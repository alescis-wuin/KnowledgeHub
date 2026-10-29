namespace KnowledgeHub.Domain.Entities;

public class EvaluationDataset
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string SourceType { get; private set; } = string.Empty;

    public DateTime CreatedUtc { get; private set; }

    public ICollection<EvaluationQuestion> Questions { get; private set; }
        = new List<EvaluationQuestion>();

    private EvaluationDataset() { }

    public EvaluationDataset(
        string name,
        string sourceType)
    {
        Id = Guid.NewGuid();
        Name = name;
        SourceType = sourceType;
        CreatedUtc = DateTime.UtcNow;
    }
}