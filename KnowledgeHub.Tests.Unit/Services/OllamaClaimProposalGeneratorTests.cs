using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Net;
using System.Text;
using System.Text.Json;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Application.Verification;
using KnowledgeHub.Infrastructure.AI;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

// MeterListener observes a process-wide Meter. These proposal telemetry tests
// must not overlap other metric collectors in xUnit parallel collections.
[CollectionDefinition("Ollama proposal telemetry isolation", DisableParallelization = true)]
public sealed class OllamaProposalTelemetryIsolationCollection
{
}

[Collection("Ollama proposal telemetry isolation")]
public sealed class OllamaClaimProposalGeneratorTests
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    private static EvidenceRetrievalSnapshot Snapshot(string content = "Document autorisé") =>
        new([new EvidenceSnapshot(DocumentId, ChunkId, content)],
            new Dictionary<Guid, string> { [DocumentId] = new string('a', 64) });

    [Fact]
    public async Task RequestUsesOnlyAuthorizedServerSnapshotAndStrictJsonInstruction()
    {
        var handler = new StubHandler("{\"message\":{\"content\":\"{\\\"schemaVersion\\\":1}\"}}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var result = await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question test", Snapshot());

        Assert.Equal("{\"schemaVersion\":1}", result);
        Assert.Equal(1, handler.Calls);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var messages = request.RootElement.GetProperty("messages");
        Assert.Contains("no markdown", messages[0].GetProperty("content").GetString()!,
            StringComparison.OrdinalIgnoreCase);
        var userContent = messages[1].GetProperty("content").GetString()!;
        var records = userContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, records.Length);
        Assert.Equal("Question: question test", records[0]);
        Assert.Equal("Authorized persisted chunks (JSON Lines):", records[1]);
        using var authorized = JsonDocument.Parse(records[2]);
        Assert.Equal(DocumentId, authorized.RootElement.GetProperty("documentId").GetGuid());
        Assert.Equal(ChunkId, authorized.RootElement.GetProperty("chunkId").GetGuid());
        Assert.Equal("Document autorisé", authorized.RootElement.GetProperty("content").GetString());
        Assert.Equal(Snapshot().AllowedChunks[ChunkId].ChunkSha256,
            authorized.RootElement.GetProperty("chunkSha256").GetString());
    }

    [Fact]
    public async Task OversizedChunkDoesNotContactOllamaOrTruncateOffsets()
    {
        var handler = new StubHandler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var result = await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot(new string('x', 25000)));
        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ReasoningOnlyResponseCannotBecomeUntrustedProposal()
    {
        var handler = new StubHandler(
            "{\"message\":{\"thinking\":\"SECRET\",\"content\":\"\"}}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        Assert.Null(await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot()));
    }

    [Fact]
    public async Task ServerFailureDoesNotReturnContent()
    {
        var handler = new StubHandler("PRIVATE", HttpStatusCode.ServiceUnavailable);
        using var capture = new TelemetryCapture();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        Assert.Null(await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot()));
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.requests" &&
            item.Value == 1 && item.Tags.GetValueOrDefault("outcome") == "error");
    }

    [Fact]
    public async Task ProposalCallRecordsBoundedChatMetricsNativeUsageAndSanitizedTrace()
    {
        const string privateText = "PRIVATE_PROPOSAL_AND_MODEL";
        const string resultJson = "{\"schemaVersion\":1}";
        var reply = JsonSerializer.Serialize(new
        {
            model = privateText,
            message = new { content = resultJson, thinking = privateText },
            total_duration = 2500000000L,
            prompt_eval_count = 12,
            eval_duration = 1000000000L,
            eval_count = 7
        });
        using var capture = new TelemetryCapture();
        using var root = new Activity("kh0084-codex-telemetry-test").Start();
        using var http = new HttpClient(new StubHandler(reply))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };

        var result = await new OllamaClaimProposalGenerator(http).GenerateAsync(
            privateText, Snapshot(privateText));

        Assert.Equal(resultJson, result);
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.requests" &&
            item.Value == 1 && item.Tags.GetValueOrDefault("operation") == "chat" &&
            item.Tags.GetValueOrDefault("outcome") == "ok");
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.native.tokens" &&
            item.Value == 12 && item.Tags.GetValueOrDefault("operation") == "chat" &&
            item.Tags.GetValueOrDefault("kind") == "prompt");
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.native.tokens" &&
            item.Value == 7 && item.Tags.GetValueOrDefault("kind") == "output");
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.native.duration" &&
            item.Value == 2500 && item.Tags.GetValueOrDefault("phase") == "total");
        var trace = Assert.Single(capture.Activities,
            activity => activity.DisplayName == "ollama.chat" &&
                activity.TraceId == root.TraceId);
        Assert.Equal(12L, trace.GetTagItem("kh.ollama.prompt_tokens"));
        Assert.Equal(7L, trace.GetTagItem("kh.ollama.output_tokens"));
        Assert.DoesNotContain(privateText, string.Join(" ", trace.TagObjects.Select(tag =>
            tag.Key + "=" + tag.Value)), StringComparison.Ordinal);
        Assert.DoesNotContain(privateText, string.Join(" ", capture.Metrics.Select(item =>
            string.Join(" ", item.Tags.Select(tag => tag.Key + "=" + tag.Value)))),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task HeadersWithoutBodyStillTimeOutWhileBuffering()
    {
        using var capture = new TelemetryCapture();
        using var root = new Activity("kh0084-codex-timeout-test").Start();
        using var http = new HttpClient(new StalledBodyHandler())
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMilliseconds(150)
        };

        var error = await Record.ExceptionAsync(() =>
            new OllamaClaimProposalGenerator(http).GenerateAsync("q", Snapshot())
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.requests" &&
            item.Value == 1 && item.Tags.GetValueOrDefault("outcome") == "error");
        var trace = Assert.Single(capture.Activities,
            activity => activity.DisplayName == "ollama.chat" &&
                activity.TraceId == root.TraceId);
        Assert.Equal("error", trace.GetTagItem("kh.operation.outcome"));
    }

    [Fact]
    public async Task CallerCancellationDuringBodyReadRemainsCancelled()
    {
        using var capture = new TelemetryCapture();
        using var http = new HttpClient(new StalledBodyHandler())
        {
            BaseAddress = new Uri("http://localhost:11434"),
            Timeout = TimeSpan.FromMinutes(3)
        };
        using var abort = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));

        var error = await Record.ExceptionAsync(() =>
            new OllamaClaimProposalGenerator(http).GenerateAsync("q", Snapshot(), abort.Token)
                .WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Contains(capture.Metrics, item => item.Name == "kh.ollama.requests" &&
            item.Value == 1 && item.Tags.GetValueOrDefault("outcome") == "cancelled");
    }

    private sealed record MetricSample(
        string Name, double Value, Dictionary<string, string> Tags);

    private sealed class TelemetryCapture : IDisposable
    {
        private readonly ConcurrentBag<MetricSample> _metrics = [];
        private readonly ConcurrentBag<Activity> _activities = [];
        private readonly MeterListener _meter = new();
        private readonly ActivityListener _activity;

        public TelemetryCapture()
        {
            _meter.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == KnowledgeHubMetrics.MeterName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };
            _meter.SetMeasurementEventCallback<long>((instrument, value, tags, _) =>
                _metrics.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _meter.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
                _metrics.Add(new MetricSample(instrument.Name, value, Copy(tags))));
            _meter.Start();

            _activity = new ActivityListener
            {
                ShouldListenTo = source => source.Name == KnowledgeHubActivities.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = activity => _activities.Add(activity)
            };
            ActivitySource.AddActivityListener(_activity);
        }

        public IReadOnlyCollection<MetricSample> Metrics => _metrics.ToArray();
        public IReadOnlyCollection<Activity> Activities => _activities.ToArray();

        public void Dispose()
        {
            _activity.Dispose();
            _meter.Dispose();
        }

        private static Dictionary<string, string> Copy(
            ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var tag in tags)
            {
                result[tag.Key] = tag.Value?.ToString() ?? string.Empty;
            }

            return result;
        }
    }

    private sealed class StalledBodyHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new StalledStream())
            });
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override Task<int> ReadAsync(
            byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            StallAsync(cancellationToken);

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            new(StallAsync(cancellationToken));

        private static async Task<int> StallAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }
    }

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
