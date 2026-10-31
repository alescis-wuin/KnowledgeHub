using System.Collections.Concurrent;
using System.Diagnostics;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Infrastructure.AI;
using Moq;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class KnowledgeHubActivitiesTests
{
    [Fact]
    public async Task RagPipeline_ProducesParentedSpansWithoutPromptOrDocumentContent()
    {
        const string privateQuestion = "PRIVATE_QUESTION_SENTINEL";
        const string privateContext = "PRIVATE_CONTEXT_SENTINEL";
        const string privateAnswer = "PRIVATE_ANSWER_SENTINEL";
        var finished = new ConcurrentBag<Activity>();
        using var listener = CreateListener(finished);
        // ActivityListeners are process-wide: other xUnit classes may execute in
        // parallel. An isolated trace prevents collecting unrelated rag.ask spans.
        using var testRoot = StartIsolatedActivity("kh0052.test.rag");

        var contextBuilder = new Mock<IContextBuilderService>();
        contextBuilder.Setup(x => x.BuildAsync(
                privateQuestion, 2, It.IsAny<CancellationToken>()))
            .Returns((string questionArg, int topKArg, CancellationToken token) =>
                KnowledgeHubActivities.TraceAsync<RetrievedContextDto>("rag.retrieve", () =>
                    Task.FromResult(new RetrievedContextDto
                    {
                        Question = privateQuestion,
                        Context = privateContext,
                        Sources = [new VectorSearchResultDto
                        {
                            Title = "PRIVATE_DOCUMENT_TITLE",
                            Content = privateContext,
                            DocumentId = Guid.NewGuid(),
                            ChunkId = Guid.NewGuid(),
                            Score = 0.9
                        }]
                    }), token));

        var chatCompletion = new Mock<IChatCompletionService>();
        chatCompletion.Setup(x => x.GenerateAnswerAsync(
                privateQuestion, privateContext, It.IsAny<CancellationToken>()))
            .ReturnsAsync(privateAnswer);

        var service = new RagService(contextBuilder.Object, chatCompletion.Object);
        var result = await service.AskAsync(privateQuestion);
        Assert.Equal(privateAnswer, result.Answer);

        var spans = finished
            .Where(x => x.TraceId == testRoot.TraceId &&
                (x.DisplayName is "rag.ask" or "rag.retrieve" or "rag.generate"))
            .GroupBy(x => x.DisplayName)
            .ToDictionary(x => x.Key, x => Assert.Single(x));
        Assert.Equal(3, spans.Count);
        var rag = spans["rag.ask"];
        Assert.Equal(testRoot.SpanId, rag.ParentSpanId);
        foreach (var child in new[] { "rag.retrieve", "rag.generate" })
        {
            Assert.Equal(rag.TraceId, spans[child].TraceId);
            Assert.Equal(rag.SpanId, spans[child].ParentSpanId);
        }

        foreach (var span in spans.Values)
        {
            Assert.True(span.Duration >= TimeSpan.Zero);
            var snapshot = span.DisplayName + " " + string.Join(" ",
                span.TagObjects.Select(x => $"{x.Key}={x.Value}"));
            Assert.DoesNotContain(privateQuestion, snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain(privateContext, snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain(privateAnswer, snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain("PRIVATE_DOCUMENT_TITLE", snapshot, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Error_UsesExceptionTypeOnly()
    {
        const string secret = "SECRET_EXCEPTION_MESSAGE_8642";
        var finished = new ConcurrentBag<Activity>();
        using var listener = CreateListener(finished);
        using var testRoot = StartIsolatedActivity("kh0052.test.error");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KnowledgeHubActivities.TraceAsync<int>("rag.retrieve", () =>
                Task.FromException<int>(new InvalidOperationException(secret))));

        var activity = Assert.Single(finished, x =>
            x.TraceId == testRoot.TraceId && x.DisplayName == "rag.retrieve");
        Assert.Equal(testRoot.SpanId, activity.ParentSpanId);
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
        Assert.Equal("InvalidOperationException", activity.GetTagItem("error.type"));
        Assert.Equal("error", activity.GetTagItem("kh.operation.outcome"));
        Assert.DoesNotContain(secret, string.Join(" ", activity.TagObjects.Select(x => x.Value)));
        Assert.Empty(activity.Events);
    }

    [Fact]
    public async Task UserCancellation_IsNotAnApplicationFailure()
    {
        var finished = new ConcurrentBag<Activity>();
        using var listener = CreateListener(finished);
        using var testRoot = StartIsolatedActivity("kh0052.test.cancel");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KnowledgeHubActivities.TraceAsync<int>("rag.retrieve", () =>
                Task.FromCanceled<int>(cancellation.Token), cancellation.Token));

        var activity = Assert.Single(finished, x =>
            x.TraceId == testRoot.TraceId && x.DisplayName == "rag.retrieve");
        Assert.Equal("cancelled", activity.GetTagItem("kh.operation.outcome"));
        Assert.Equal(ActivityStatusCode.Unset, activity.Status);
    }

    private static Activity StartIsolatedActivity(string name)
    {
        var parentContext = new ActivityContext(
            ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
            ActivityTraceFlags.Recorded);
        return KnowledgeHubActivities.Source.StartActivity(
            name, ActivityKind.Internal, parentContext)
            ?? throw new InvalidOperationException("Test trace was not sampled.");
    }

    private static ActivityListener CreateListener(ConcurrentBag<Activity> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == KnowledgeHubActivities.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }
}
