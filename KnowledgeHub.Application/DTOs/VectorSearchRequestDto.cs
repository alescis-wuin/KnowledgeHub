namespace KnowledgeHub.Application.DTOs;

public sealed class VectorSearchRequestDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;

    public int TopK
    {
        get;
        set;
    } = 5;
}