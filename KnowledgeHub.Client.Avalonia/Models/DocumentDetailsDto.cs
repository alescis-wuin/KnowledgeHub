namespace KnowledgeHub.Client.Avalonia.Models;

public class DocumentDetailsDto
{
    public Guid Id { get; set; }

    public string Title { get; set; }
        = string.Empty;

    public string Content { get; set; }
        = string.Empty;

    public string? Summary { get; set; }

    public string? SearchKeywords { get; set; }

    public string? Category { get; set; }

    public string? Source { get; set; }

    public List<string> Tags { get; set; }
        = [];

    public List<ChunkDto> Chunks { get; set; }
        = [];

    public DateTime CreatedAt { get; set; }
}