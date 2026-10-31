using System.Collections.Concurrent;
using System.Diagnostics.Metrics;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Infrastructure.AI;
using Moq;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class KnowledgeHubMetricsTests
{
    [Fact]
    public async Task RagWithoutSources_RecordsNoContextWithoutPrivateText()
    {
        const string secret = "PRIVATE_METRICS_QUESTION_7621";
        using var measurements = new MetricCapture();
        var context = new Mock<IContextBuilderService>();
        context.Setup(service => service.BuildAsync(secret, 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedContextDto
            {
                Question = secret,
                Context = string.Empty,
                Sources = []
            });
        var chat = new Mock<IChatCompletionService>(MockBehavior.Strict);

        var result = await new RagService(context.Object, chat.Object).AskAsync(secret);
        Assert.Empty(result.Sources);

        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "no_context" &&
            sample.Value == 1);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.duration" &&
            sample.Tags.GetValueOrDefault("outcome") == "no_context" &&
            sample.Value >= 0);
        Assert.All(measurements.Values, sample =>
            Assert.DoesNotContain(secret, sample.ToString(), StringComparison.Ordinal));
        chat.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RagWithSource_RecordsSuccessAndGenerationStage()
    {
        using var measurements = new MetricCapture();
        var context = new Mock<IContextBuilderService>();
        context.Setup(service => service.BuildAsync("q", 2, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RetrievedContextDto
            {
                Question = "q",
                Context = "PRIVATE_CONTEXT_881",
                Sources = [new VectorSearchResultDto
                {
                    DocumentId = Guid.NewGuid(),
                    ChunkId = Guid.NewGuid(),
                    Content = "PRIVATE_CONTEXT_881",
                    Title = "PRIVATE_TITLE_881",
                    Score = 0.9
                }]
            });
        var chat = new Mock<IChatCompletionService>();
        chat.Setup(service => service.GenerateAnswerAsync(
                "q", "PRIVATE_CONTEXT_881", It.IsAny<CancellationToken>()))
            .ReturnsAsync("PRIVATE_ANSWER_881");

        var response = await new RagService(context.Object, chat.Object).AskAsync("q");
        Assert.Equal("PRIVATE_ANSWER_881", response.Answer);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "ok");
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.stage.duration" &&
            sample.Tags.GetValueOrDefault("stage") == "generate" &&
            sample.Tags.GetValueOrDefault("outcome") == "ok");
        var metricsText = string.Join(" ", measurements.Values.Select(sample => sample.ToString()));
        Assert.DoesNotContain("PRIVATE_CONTEXT_881", metricsText, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_TITLE_881", metricsText, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_ANSWER_881", metricsText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task RagFailure_RecordsErrorWithoutExceptionMessage()
    {
        const string secret = "PRIVATE_EXCEPTION_MESSAGE_9008";
        using var measurements = new MetricCapture();
        var context = new Mock<IContextBuilderService>();
        context.Setup(service => service.BuildAsync("q", 2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException(secret));
        var chat = new Mock<IChatCompletionService>(MockBehavior.Strict);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new RagService(context.Object, chat.Object).AskAsync("q"));
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "error");
        Assert.DoesNotContain(secret,
            string.Join(" ", measurements.Values.Select(sample => sample.ToString())),
            StringComparison.Ordinal);
        chat.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RagCancellation_RecordsCancelledNotError()
    {
        using var measurements = new MetricCapture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var context = new Mock<IContextBuilderService>();
        context.Setup(service => service.BuildAsync("q", 2, It.IsAny<CancellationToken>()))
            .Returns((string questionValue, int topKValue, CancellationToken token) =>
                Task.FromCanceled<RetrievedContextDto>(token));
        var chat = new Mock<IChatCompletionService>(MockBehavior.Strict);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new RagService(context.Object, chat.Object).AskAsync("q", cancellation.Token));
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "cancelled");
        chat.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Retrieval_RecordsResultCount_WithoutDocumentIdentifiers()
    {
        using var measurements = new MetricCapture();
        var id = Guid.NewGuid();
        var result = await KnowledgeHubMetrics.MeasureRetrievalAsync(
            () => Task.FromResult<IReadOnlyCollection<Guid>>([id]));

        Assert.Single(result);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.retrieval.results" && sample.Value == 1);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.retrieval.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "ok");
        Assert.All(measurements.Values, sample =>
            Assert.DoesNotContain(id.ToString(), sample.ToString(), StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailureAndCancellation_ProduceDistinctLowCardinalityOutcomes()
    {
        using var measurements = new MetricCapture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            KnowledgeHubMetrics.MeasureOllamaAsync("chat", () =>
                Task.FromException<string>(new InvalidOperationException("SECRET_EXCEPTION_882"))));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KnowledgeHubMetrics.MeasureOllamaAsync("embedding", () =>
                Task.FromCanceled<float[]>(cancellation.Token), cancellation.Token));

        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.ollama.requests" &&
            sample.Tags.GetValueOrDefault("operation") == "chat" &&
            sample.Tags.GetValueOrDefault("outcome") == "error");
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.ollama.requests" &&
            sample.Tags.GetValueOrDefault("operation") == "embedding" &&
            sample.Tags.GetValueOrDefault("outcome") == "cancelled");
        Assert.DoesNotContain("SECRET_EXCEPTION_882",
            string.Join(" ", measurements.Values.Select(sample => sample.ToString())),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task StageMeasurements_CoverSuccessAndCancellation()
    {
        using var measurements = new MetricCapture();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Equal(3, KnowledgeHubMetrics.MeasureStage("context", () => 3));
        Assert.Equal(4, await KnowledgeHubMetrics.MeasureStageAsync(
            "retrieve", () => Task.FromResult(4)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            KnowledgeHubMetrics.MeasureStageAsync(
                "retrieve", () => Task.FromCanceled<int>(cancellation.Token),
                cancellation.Token));

        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.stage.duration" &&
            sample.Tags.GetValueOrDefault("stage") == "context" &&
            sample.Tags.GetValueOrDefault("outcome") == "ok");
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.rag.stage.duration" &&
            sample.Tags.GetValueOrDefault("stage") == "retrieve" &&
            sample.Tags.GetValueOrDefault("outcome") == "cancelled");
    }

    [Fact]
    public void CacheMetrics_SeparateHitMissLoadAndInvalidation()
    {
        using var measurements = new MetricCapture();
        KnowledgeHubMetrics.RecordCacheLookup(hit: true);
        KnowledgeHubMetrics.RecordCacheLookup(hit: false);
        KnowledgeHubMetrics.RecordCacheLoad("ok", 5.2, 17);
        KnowledgeHubMetrics.RecordCacheInvalidation();

        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.vector_cache.lookups" &&
            sample.Tags.GetValueOrDefault("result") == "hit");
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.vector_cache.lookups" &&
            sample.Tags.GetValueOrDefault("result") == "miss");
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.vector_cache.load.duration" && sample.Value == 5.2);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.vector_cache.entries" && sample.Value == 17);
        Assert.Contains(measurements.Values, sample =>
            sample.Instrument == "kh.vector_cache.invalidations" && sample.Value == 1);
    }

    [Fact]
    public void InvalidDynamicLabels_AreRejectedBeforeRecording()
    {
        using var measurements = new MetricCapture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KnowledgeHubMetrics.RecordRagRequest("USER_PROMPT_AS_OUTCOME", 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KnowledgeHubMetrics.MeasureStage("PRIVATE_STAGE", () => 1));
        // Invalid operation labels are rejected synchronously, before a Task is returned.
        // An explicit Action prevents xUnit from selecting obsolete Func<Task> overloads.
        Action invalidOllamaOperation = () =>
            _ = KnowledgeHubMetrics.MeasureOllamaAsync(
                "USER_MODEL_NAME", () => Task.FromResult(1));
        Assert.Throws<ArgumentOutOfRangeException>(invalidOllamaOperation);
        Assert.DoesNotContain(measurements.Values, sample =>
            sample.ToString().Contains("PRIVATE_STAGE", StringComparison.Ordinal));
    }

    private sealed record MetricSample(
        string Instrument, double Value, Dictionary<string, string> Tags)
    {
        public override string ToString() =>
            Instrument + "=" + Value.ToString(System.Globalization.CultureInfo.InvariantCulture) +
            " " + string.Join(" ", Tags.Select(tag => tag.Key + "=" + tag.Value));
    }

    private sealed class MetricCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<MetricSample> _values = [];

        public MetricCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == KnowledgeHubMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                _values.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                _values.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _listener.Start();
        }

        public IReadOnlyCollection<MetricSample> Values => _values.ToArray();

        public void Dispose() => _listener.Dispose();

        private static Dictionary<string, string> Copy(
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                snapshot[tag.Key] = Convert.ToString(
                    tag.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
            }
            return snapshot;
        }
    }
}
