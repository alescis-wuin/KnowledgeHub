namespace KnowledgeHub.Application.Interfaces;

public interface IChatCompletionService
{
    Task<string>
        GenerateAnswerAsync(
            string question,
            string context,
            CancellationToken cancellationToken =
                default);
}