namespace KnowledgeHub.Client.Models;

public sealed class AskQuestionResponseDto
{
    public string Question
    {
        get;
        set;
    }
    = string.Empty;

    public string Answer
    {
        get;
        set;
    }
    = string.Empty;

    public IReadOnlyCollection<
        SourceReferenceDto> Sources
    {
        get;
        set;
    }
    = [];
}