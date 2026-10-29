namespace KnowledgeHub.Domain.Entities;

public class KnowledgeDocumentTag
{
    public Guid DocumentId { get; set; }

    public KnowledgeDocument Document { get; set; }
        = null!;

    public Guid TagId { get; set; }

    public Tag Tag { get; set; }
        = null!;
}