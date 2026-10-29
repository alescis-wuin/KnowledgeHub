namespace KnowledgeHub.Client.Models;

public sealed class SourceReferenceDto
{
    public string Title
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

    public int RelevancePercent
    {
        get;
        set;
    }

    public string Excerpt
    {
        get;
        set;
    }
    = string.Empty;
}