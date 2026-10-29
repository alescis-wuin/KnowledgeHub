namespace KnowledgeHub.Domain.Entities;

public class Tag
{
    public Guid Id { get; private set; }

    public string Name { get; private set; } =
        string.Empty;

    public ICollection<KnowledgeDocumentTag> DocumentTags
        {
            get;
            private set;
        }
        = new List<KnowledgeDocumentTag>();

    private Tag()
    {
    }

    public Tag(string name)
    {
        Id = Guid.NewGuid();

        Name = name.Trim();
    }
}