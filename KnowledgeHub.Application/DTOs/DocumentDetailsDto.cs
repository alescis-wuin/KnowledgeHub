using KnowledgeHub.Application.DTOs;

public class DocumentDetailsDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } =
        string.Empty;

    public string Content { get; set; } =
        string.Empty;

    public string? Category { get; set; }

    public string? Source { get; set; }

    public IReadOnlyCollection<string> Tags
    {
        get;
        set;
    }
    = [];

    public IReadOnlyCollection<ChunkDto> Chunks
    {
        get;
        set;
    }
    = [];

    public DateTime CreatedAt
    {
        get;
        set;
    }
}