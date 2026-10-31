namespace KnowledgeHub.Application.DTOs;

public class DocumentDto
{
    public Guid Id { get; set; }

    public Guid? ExternalId { get; set; }

    public string? ContentSha256 { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public List<string> Tags
    {
        get;
        set;
    }
    = [];
}