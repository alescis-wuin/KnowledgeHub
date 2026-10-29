namespace KnowledgeHub.Application.DTOs;

public sealed class VectorSearchResultDto
{
    public Guid ChunkId
    {
        get;
        set;
    }

    public Guid DocumentId
    {
        get;
        set;
    }

    public string Title
    {
        get;
        set;
    }
    = string.Empty;

    public string Content
    {
        get;
        set;
    }
    = string.Empty;

    public double Score
    {
        get;
        set;
    }
}