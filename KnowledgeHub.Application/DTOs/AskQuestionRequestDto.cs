namespace KnowledgeHub.Application.DTOs;

public sealed class AskQuestionRequestDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;
}