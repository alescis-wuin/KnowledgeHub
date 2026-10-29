namespace KnowledgeHub.Application.DTOs;

public class CreateKnowledgeSourceDto
{
    public string Name { get; set; }
        = string.Empty;

    public string? Url { get; set; }
}