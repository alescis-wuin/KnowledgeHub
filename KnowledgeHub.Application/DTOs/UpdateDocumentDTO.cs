namespace KnowledgeHub.Application.DTOs;

public sealed class UpdateDocumentDto
{
    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;
}