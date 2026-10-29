namespace KnowledgeHub.Domain.Entities;

public class KnowledgeSource
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } =
        string.Empty;

    public string? Url { get; private set; }

    private KnowledgeSource()
    {
    }

    public KnowledgeSource(
        string name,
        string? url = null)
    {
        Id = Guid.NewGuid();

        Name = name.Trim();

        Url = url;
    }
}