namespace KnowledgeHub.Application.DTOs;

public sealed class RetrievedContextDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;

    public string Context
    {
        get;
        set;
    }
    = string.Empty;

    public IReadOnlyCollection<
        VectorSearchResultDto> Sources
    {
        get;
        set;
    }
    = [];
}