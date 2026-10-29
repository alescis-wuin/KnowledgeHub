namespace KnowledgeHub.Client.Avalonia.Models;

public class SourceDto
{
    public Guid Id { get; set; }

    public string Name { get; set; }
        = string.Empty;

    public string? Url { get; set; }
}