namespace KnowledgeHub.Client.Models;

public sealed class AskQuestionRequestDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;
}