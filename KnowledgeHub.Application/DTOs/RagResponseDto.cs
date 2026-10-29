namespace KnowledgeHub.Application.DTOs;

public class RagResponseDto
{
    public string Answer { get; set; }
        = string.Empty;

    public IReadOnlyCollection<string>
        Sources
    {
        get;
        set;
    }
    = [];
}