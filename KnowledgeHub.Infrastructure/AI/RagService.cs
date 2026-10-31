using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace KnowledgeHub.Infrastructure.AI;

public sealed class RagService
{
    private readonly
        IContextBuilderService
        _contextBuilder;

    private readonly
        IChatCompletionService
        _chatCompletionService;
    private readonly ILogger<RagService> _logger;

    private static readonly EventId SkippedEvent = new(53001, "RagSkipped");
    private static readonly EventId CompletedEvent = new(53002, "RagCompleted");

    public RagService(
        IContextBuilderService
            contextBuilder,
        IChatCompletionService
            chatCompletionService,
        ILogger<RagService>? logger = null)
    {
        _contextBuilder =
            contextBuilder;

        _chatCompletionService =
            chatCompletionService;
        _logger = logger ?? NullLogger<RagService>.Instance;
    }


    public async Task<AskQuestionResponseDto> AskAsync(
        string question,
        CancellationToken cancellationToken = default)
    {
        return await KnowledgeHubActivities.TraceAsync("rag.ask", async () =>
        {
        var stopwatch = Stopwatch.StartNew();

        var context = await _contextBuilder.BuildAsync(
            question,
            2,
            cancellationToken);

        Activity.Current?.SetTag("kh.rag.source_count", context.Sources.Count);
        if (context.Sources.Count == 0 ||
            string.IsNullOrWhiteSpace(context.Context))
        {
            stopwatch.Stop();

            _logger.LogInformation(SkippedEvent,
                "RAG skipped: no usable context; DurationMs={DurationMs}",
                Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));

            return new AskQuestionResponseDto
            {
                Question = question,
                Answer =
                    "Aucune source exploitable dans la base de connaissances ne permet actuellement de répondre à cette question.",
                Sources = []
            };
        }

        var answer = await KnowledgeHubActivities.TraceAsync<string>(
            "rag.generate",
            () => _chatCompletionService.GenerateAnswerAsync(
                question, context.Context, cancellationToken),
            cancellationToken);

        stopwatch.Stop();

        _logger.LogInformation(CompletedEvent,
            "RAG completed; SourceCount={SourceCount}; DurationMs={DurationMs}",
            context.Sources.Count,
            Math.Round(stopwatch.Elapsed.TotalMilliseconds, 2));

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
        }, cancellationToken);
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