namespace KnowledgeHub.Client.Avalonia.Models;

public sealed class AskQuestionRequestDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;
}