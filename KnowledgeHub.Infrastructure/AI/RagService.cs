using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using System.Diagnostics;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class RagService
{
    private readonly
        IContextBuilderService
        _contextBuilder;

    private readonly
        IChatCompletionService
        _chatCompletionService;

    public RagService(
        IContextBuilderService
            contextBuilder,
        IChatCompletionService
            chatCompletionService)
    {
        _contextBuilder =
            contextBuilder;

        _chatCompletionService =
            chatCompletionService;
    }


    public async Task<AskQuestionResponseDto> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var context = await _contextBuilder.BuildAsync(
            question,
            2,
            cancellationToken);

        if (context.Sources.Count == 0 ||
            string.IsNullOrWhiteSpace(context.Context))
        {
            stopwatch.Stop();

            Console.WriteLine(
                $"RAG skipped: no usable context ({stopwatch.Elapsed})");

            return new AskQuestionResponseDto
            {
                Question = question,
                Answer =
                    "Aucune source exploitable dans la base de connaissances ne permet actuellement de répondre à cette question.",
                Sources = []
            };
        }

        var answer = await _chatCompletionService.GenerateAnswerAsync(
            question,
            context.Context,
            cancellationToken);

        stopwatch.Stop();

        Console.WriteLine(
            $"RAG completed in {stopwatch.Elapsed}");

        return new AskQuestionResponseDto
        {
            Question = question,
            Answer = answer,
            Sources = context.Sources
                .Select(source => new SourceReferenceDto
                {
                    Title = source.Title,
                    Score = source.Score,
                    RelevancePercent =
                        (int)Math.Round(source.Score * 100),
                    Excerpt = BuildExcerpt(source.Content)
                })
                .ToList()
        };
    }


    private static string BuildExcerpt(
        string content)
    {
        const int maxLength = 250;

        if (
            string.IsNullOrWhiteSpace(
                content))
        {
            return string.Empty;
        }

        if (
            content.Length <= maxLength)
        {
            return content;
        }

        var excerpt =
            content.Substring(
                0,
                maxLength);

        var lastSpace =
            excerpt.LastIndexOf(' ');

        if (lastSpace > 0)
        {
            excerpt =
                excerpt.Substring(
                    0,
                    lastSpace);
        }

        return excerpt + "...";
    }
}