namespace KnowledgeHub.Client.Avalonia.Models;

public class ChunkDto
{
    public Guid Id { get; set; }

    public Guid DocumentId { get; set; }

    public int ChunkIndex { get; set; }

    public int TokenCount { get; set; }

    public string Content { get; set; }
        = string.Empty;

    public DateTime CreatedAt { get; set; }
}