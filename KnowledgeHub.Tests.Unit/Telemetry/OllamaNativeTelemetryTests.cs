using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using System.Net;
using System.Text;
using KnowledgeHub.Api.Telemetry;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Infrastructure.AI;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class OllamaNativeTelemetryTests
{
    [Fact]
    public async Task Chat_RecordsOllamaNativeTokensDurationsAndOutputSpeed()
    {
        const string secret = "PRIVATE_KH006_RAW_THINKING_MODEL_AND_PROMPT";
        const string body = """
            {
              "model": "PRIVATE_KH006_RAW_THINKING_MODEL_AND_PROMPT",
              "message": { "content": "Safe reply", "thinking": "PRIVATE_KH006_RAW_THINKING_MODEL_AND_PROMPT" },
              "total_duration": 4500000000,
              "load_duration": 400000000,
              "prompt_eval_duration": 1000000000,
              "eval_duration": 2000000000,
              "prompt_eval_count": 123,
              "prompt_eval_cached_count": 25,
              "eval_count": 50
            }
            """;
        using var capture = new MeterCapture();
        using var activities = new ActivityCapture();
        using var root = new Activity("kh006-test-chat").Start();
        using var client = CreateClient(body);

        var result = await new OllamaChatCompletionService(client)
            .GenerateAnswerAsync(secret, secret);

        Assert.Equal("Safe reply", result);
        var metrics = capture.Values;
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.duration", 4500,
            "chat", "phase", "total"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.duration", 400,
            "chat", "phase", "load"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.duration", 1000,
            "chat", "phase", "prompt_eval"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.duration", 2000,
            "chat", "phase", "eval"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.tokens", 123,
            "chat", "kind", "prompt"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.tokens", 25,
            "chat", "kind", "cached_prompt"));
        Assert.Contains(metrics, sample => Matches(sample, "kh.ollama.native.tokens", 50,
            "chat", "kind", "output"));
        Assert.Contains(metrics, sample => sample.Instrument ==
            "kh.ollama.native.output.tokens_per_second" &&
            sample.Tags.GetValueOrDefault("operation") == "chat" && sample.Value == 25);

        var span = Assert.Single(activities.Finished, activity =>
            activity.TraceId == root.TraceId && activity.DisplayName == "ollama.chat");
        Assert.Equal(123L, span.GetTagItem("kh.ollama.prompt_tokens"));
        Assert.Equal(50L, span.GetTagItem("kh.ollama.output_tokens"));
        Assert.Equal(25d, span.GetTagItem("kh.ollama.output_tokens_per_second"));
        Assert.Equal(4500d, span.GetTagItem("kh.ollama.total_duration_ms"));
        Assert.DoesNotContain(secret, Snapshot(metrics, span), StringComparison.Ordinal);
        Assert.DoesNotContain("thinking", Snapshot(metrics, span), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("model", Snapshot(metrics, span), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LegacyEmbedding_RecordsOnlyFieldsActuallyReturned()
    {
        const string body = """
            {
              "embedding": [0.25, 0.5],
              "model": "PRIVATE_MODEL_NAME",
              "total_duration": 1500000000,
              "load_duration": 100000000,
              "prompt_eval_count": 17
            }
            """;
        using var capture = new MeterCapture();
        using var client = CreateClient(body);

        var result = await new OllamaEmbeddingService(client)
            .GenerateEmbeddingAsync("PRIVATE_EMBEDDING_INPUT");

        Assert.Equal(2, result.Length);
        Assert.Equal(0.25f, result[0]);
        Assert.Contains(capture.Values, sample => Matches(sample,
            "kh.ollama.native.duration", 1500, "embedding", "phase", "total"));
        Assert.Contains(capture.Values, sample => Matches(sample,
            "kh.ollama.native.duration", 100, "embedding", "phase", "load"));
        Assert.Contains(capture.Values, sample => Matches(sample,
            "kh.ollama.native.tokens", 17, "embedding", "kind", "prompt"));
        Assert.DoesNotContain(capture.Values, sample =>
            sample.Instrument == "kh.ollama.native.output.tokens_per_second" &&
            sample.Tags.GetValueOrDefault("operation") == "embedding");
        Assert.DoesNotContain("PRIVATE_MODEL_NAME", Snapshot(capture.Values),
            StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_EMBEDDING_INPUT", Snapshot(capture.Values),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task MissingUsage_DoesNotSynthesizeZeroOrChangeLegacyAnswer()
    {
        using var capture = new MeterCapture();
        using var client = CreateClient("""{"message":{"content":"Answer"}}""");
        var answer = await new OllamaChatCompletionService(client)
            .GenerateAnswerAsync("q", "context");

        Assert.Equal("Answer", answer);
        Assert.DoesNotContain(capture.Values, IsNative);
        Assert.Contains(capture.Values, sample => sample.Instrument == "kh.ollama.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "ok");
    }

    [Fact]
    public async Task MalformedOptionalValues_AreIgnoredWithoutBreakingValidAnswer()
    {
        const string body = """
            {
              "message": { "content": "Valid" },
              "model": "PRIVATE_MODEL_KH006",
              "total_duration": -1,
              "load_duration": "not a number",
              "prompt_eval_count": 9223372036854775808,
              "prompt_eval_cached_count": null,
              "eval_count": 1.5,
              "eval_duration": 0
            }
            """;
        using var capture = new MeterCapture();
        using var client = CreateClient(body);
        var answer = await new OllamaChatCompletionService(client)
            .GenerateAnswerAsync("q", "context");

        Assert.Equal("Valid", answer);
        Assert.Contains(capture.Values, sample => Matches(sample,
            "kh.ollama.native.duration", 0, "chat", "phase", "eval"));
        Assert.DoesNotContain(capture.Values, sample =>
            sample.Instrument == "kh.ollama.native.tokens");
        Assert.DoesNotContain(capture.Values, sample =>
            sample.Instrument == "kh.ollama.native.output.tokens_per_second");
        Assert.DoesNotContain("PRIVATE_MODEL_KH006", Snapshot(capture.Values),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnusableChatAnswer_DoesNotPublishNativeUsage()
    {
        using var capture = new MeterCapture();
        using var client = CreateClient("""
            {"message":{"content":"","thinking":"PRIVATE_COT"},
             "total_duration":1230000000,"eval_count":42}
            """);
        var service = new OllamaChatCompletionService(client);

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GenerateAnswerAsync("q", "context"));
        Assert.DoesNotContain(capture.Values, IsNative);
        Assert.Contains(capture.Values, sample => sample.Instrument == "kh.ollama.requests" &&
            sample.Tags.GetValueOrDefault("outcome") == "error");
    }

    [Fact]
    public async Task HttpFailure_DoesNotPublishNativeUsage()
    {
        using var capture = new MeterCapture();
        using var client = CreateClient("{}", HttpStatusCode.InternalServerError);
        var service = new OllamaChatCompletionService(client);

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GenerateAnswerAsync("q", "context"));
        Assert.DoesNotContain(capture.Values, IsNative);
    }

    [Fact]
    public void UnsafeTraceAttributes_AreRemovedWithoutDiscardingNumericUsage()
    {
        using var activity = new Activity("ollama.chat").Start();
        activity.SetTag("kh.ollama.prompt_tokens", 42L);
        activity.SetTag("kh.ollama.total_duration_ms", 1500d);
        activity.SetTag("model", "PRIVATE_MODEL_KH006");
        activity.SetTag("url.full", "https://sensitive.invalid/?secret=PRIVATE");
        activity.SetTag("gen_ai.input.messages", "PRIVATE_PROMPT_KH006");

        new SafeTraceProcessor().OnEnd(activity);

        Assert.Equal(42L, activity.GetTagItem("kh.ollama.prompt_tokens"));
        Assert.Equal(1500d, activity.GetTagItem("kh.ollama.total_duration_ms"));
        Assert.Null(activity.GetTagItem("model"));
        Assert.Null(activity.GetTagItem("url.full"));
        Assert.Null(activity.GetTagItem("gen_ai.input.messages"));
    }

    [Fact]
    public void NativeUsage_RejectsDynamicOperationLabels()
    {
        using var capture = new MeterCapture();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            KnowledgeHubMetrics.RecordOllamaNativeUsage(
                "PRIVATE_MODEL", new OllamaNativeUsage(1, null, null, null,
                    null, null, null)));
        Assert.DoesNotContain(capture.Values, IsNative);
    }

    private static bool Matches(
        MetricSample sample, string name, double value, string operation,
        string labelName, string labelValue) =>
        sample.Instrument == name && sample.Value == value &&
        sample.Tags.GetValueOrDefault("operation") == operation &&
        sample.Tags.GetValueOrDefault(labelName) == labelValue;

    private static bool IsNative(MetricSample sample) =>
        sample.Instrument.StartsWith("kh.ollama.native.", StringComparison.Ordinal);

    private static string Snapshot(
        IEnumerable<MetricSample> samples, Activity? activity = null) =>
        string.Join(" ", samples.Select(sample => sample.ToString())) +
        (activity is null ? "" : " " + string.Join(" ", activity.TagObjects.Select(
            tag => tag.Key + "=" + tag.Value)));

    private static HttpClient CreateClient(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(new JsonHandler(body, status))
        {
            BaseAddress = new Uri("http://localhost:11434/")
        };

    private sealed class JsonHandler(string body, HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private sealed class ActivityCapture : IDisposable
    {
        private readonly ConcurrentBag<Activity> _finished = [];
        private readonly ActivityListener _listener;

        public ActivityCapture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == KnowledgeHubActivities.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _finished.Add(activity)
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyCollection<Activity> Finished => _finished.ToArray();

        public void Dispose() => _listener.Dispose();
    }

    private sealed record MetricSample(
        string Instrument, double Value, Dictionary<string, string> Tags)
    {
        public override string ToString() => Instrument + "=" +
            Value.ToString(CultureInfo.InvariantCulture) + " " +
            string.Join(" ", Tags.Select(tag => tag.Key + "=" + tag.Value));
    }

    private sealed class MeterCapture : IDisposable
    {
        private readonly MeterListener _listener = new();
        private readonly ConcurrentBag<MetricSample> _samples = [];

        public MeterCapture()
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == KnowledgeHubMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                _samples.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                _samples.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _listener.Start();
        }

        public IReadOnlyCollection<MetricSample> Values => _samples.ToArray();

        public void Dispose() => _listener.Dispose();

        private static Dictionary<string, string> Copy(
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                values[tag.Key] = Convert.ToString(
                    tag.Value, CultureInfo.InvariantCulture) ?? "";
            }
            return values;
        }
    }
}
