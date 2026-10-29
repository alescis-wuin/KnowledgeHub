namespace KnowledgeHub.Application.DTOs;

public class KnowledgeSourceDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string? Url { get; set; }
}