using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Infrastructure.AI;
using KnowledgeHub.Tests.Integration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace KnowledgeHub.Observability.Verify;

/// <summary>
/// Isolated, reproducible, synthetic API + real OTLP/HTTP verification.
/// This program neither contacts the user's Ollama nor modifies the user's database.
/// Raw private prompts and exported protobuf are never written to the evidence report.
/// </summary>
internal static class Program
{
    private const string Question = "KH0054_PRIVATE_QUESTION_2048";
    private const string Document = "KH0054_PRIVATE_DOCUMENT_3024";
    private const string Title = "KH0054_PRIVATE_TITLE_1776";
    private const string Answer = "KH0054_PRIVATE_ANSWER_4048";
    private const string TraceId = "41a2b3c4d5e6f708192a3b4c5d6e7f80";
    private const string ParentSpanId = "f0e1d2c3b4a59687";
    private static readonly string[] Secrets = [Question, Document, Title, Answer];
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static async Task<int> Main(string[] args)
    {
        if (args.Length != 1 || !Path.IsPathFullyQualified(args[0]))
        {
            Console.Error.WriteLine("KH0054: require an absolute evidence output directory.");
            return 2;
        }

        var reportDirectory = Path.GetFullPath(args[0]);
        if (!Directory.Exists(reportDirectory))
        {
            Console.Error.WriteLine("KH0054: evidence directory does not exist.");
            return 2;
        }

        try
        {
            await ExecuteAsync(reportDirectory);
            Console.WriteLine("KH0054_E2E_RESULT=pass");
            return 0;
        }
        catch (VerificationException exception)
        {
            // Fixed, reviewed assertions only: no external exception messages.
            Console.Error.WriteLine("KH0054_E2E_RESULT=failed; Check=" + exception.Message);
            return 1;
        }
        catch (Exception exception)
        {
            // Only the exception category; infrastructure errors can contain credentials.
            Console.Error.WriteLine("KH0054_E2E_RESULT=failed; ErrorType=" +
                exception.GetType().Name);
            return 1;
        }
    }

    private static async Task ExecuteAsync(string reportDirectory)
    {
        await using var collector = new LoopbackCollector(Answer);
        // For minimal-hosting applications, WebApplicationFactory's deferred
        // ConfigureAppConfiguration callbacks are applied only after the app
        // calls WebApplicationBuilder.Build(). Program.cs reads builder.Configuration
        // to register tracing and metrics BEFORE Build, so those callbacks alone
        // cannot activate the OTLP exporters. Override process-local environment
        // settings before constructing each test host instead. This process is
        // dedicated to the verifier and prior values are restored on exit.
        using var telemetryEnvironment = new ScopedTelemetryEnvironment();
        var databaseHost = new CustomWebApplicationFactory();
        try
        {
            await databaseHost.InitializeAsync();
            // Measure baseline before starting any instrumented host. The process
            // still contains the collector and database infrastructure, and CPU/RSS
            // results are marked accordingly in the emitted report.
            BenchResult baseline;
            telemetryEnvironment.SetForHost(collector.Url, enabled: false);
            using (var baselineHost = CreateHost(databaseHost, collector, enabled: false))
            using (var baselineClient = CreateClient(baselineHost))
            {
                baseline = await BenchmarkAsync(baselineClient, "disabled", 5, 25);
            }
            telemetryEnvironment.SetForHost(collector.Url, enabled: true);
            using var observedHost = CreateHost(databaseHost, collector, enabled: true);
            using var observedClient = CreateClient(observedHost);
            // Verify the final host configuration as well as the real OTLP
            // traffic; no exporter existence or success is inferred from config.
            var effectiveConfiguration = observedHost.Services
                .GetRequiredService<IConfiguration>();
            Require(effectiveConfiguration.GetValue<bool>("Tracing:Enabled") &&
                effectiveConfiguration.GetValue<bool>("Metrics:Enabled"),
                "observability configuration not enabled for test host");
            Require(effectiveConfiguration["Tracing:OtlpEndpoint"] ==
                collector.Url + "v1/traces" &&
                effectiveConfiguration["Metrics:OtlpEndpoint"] ==
                collector.Url + "v1/metrics",
                "OTLP collector endpoints were not configured for test host");

            var correlationId = "kh0054-" + Guid.NewGuid().ToString("N");
            await SendAsync(observedClient, correlationId, traced: true);

            var exported = await collector.WaitForTelemetryAsync(
                TimeSpan.FromSeconds(25), TraceId, "kh.rag.requests", "kh.ollama.requests");

            var traceSpans = exported.Spans.Where(s => s.TraceId == TraceId).ToList();
            var server = SingleSpan(traceSpans, "http.server.request");
            var rag = SingleSpan(traceSpans, "rag.ask");
            var retrieve = SingleSpan(traceSpans, "rag.retrieve");
            var context = SingleSpan(traceSpans, "rag.context");
            var generate = SingleSpan(traceSpans, "rag.generate");
            var ollama = SingleSpan(traceSpans, "ollama.chat");
            var outbound = SingleSpan(traceSpans, "http.client.request");

            Require(rag.ParentSpanId == server.SpanId, "rag.ask has no server parent");
            Require(retrieve.ParentSpanId == rag.SpanId, "rag.retrieve has wrong parent");
            Require(context.ParentSpanId == rag.SpanId, "rag.context has wrong parent");
            Require(generate.ParentSpanId == rag.SpanId, "rag.generate has wrong parent");
            Require(ollama.ParentSpanId == generate.SpanId, "ollama.chat has wrong parent");
            Require(outbound.ParentSpanId == ollama.SpanId, "HTTP client has wrong parent");
            Require(collector.HasOutboundTraceparent(TraceId, outbound.SpanId),
                "W3C traceparent was not forwarded to the fake Ollama HTTP backend");
            Require(server.ParentSpanId == ParentSpanId, "incoming W3C parent not propagated");

            var logMatched = FindSafeLogCorrelation(correlationId, TraceId);
            Require(logMatched, "structured HTTP log lacks correlation and trace IDs");
            Require(!collector.ContainsAny(Secrets), "private synthetic data found in exported OTLP");

            // Warmed, paired measurements in the SAME process. Results are descriptive,
            // not hard thresholds: process-wide CPU/memory include the local collector.
            var enabled = await BenchmarkAsync(observedClient, "enabled", 5, 25);
            Require(!collector.ContainsAny(Secrets), "private data emitted during benchmark");

            var report = new
            {
                schema_version = 1,
                milestone = "KH-005.4",
                base_commit = "08c2c3d0491d63d2849ff995fb173938f8f1e050",
                collected_utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
                environment = new
                {
                    dotnet = Environment.Version.ToString(),
                    os = System.Runtime.InteropServices.RuntimeInformation.OSDescription,
                    processor_count = Environment.ProcessorCount
                },
                request = new
                {
                    method = "POST",
                    route = "/api/rag/ask",
                    status = 200,
                    trace_id = TraceId,
                    correlation_id = correlationId,
                    log_trace_correlation = logMatched,
                    otlp_transport = "HTTP/protobuf loopback"
                },
                exported = new
                {
                    trace_requests = collector.Count("/v1/traces"),
                    metrics_requests = collector.Count("/v1/metrics"),
                    verified_spans = traceSpans.Select(s => s.Name).OrderBy(s => s).ToArray(),
                    required_metrics = new[] { "kh.rag.requests", "kh.ollama.requests" },
                    private_sentinels_absent = true
                },
                benchmark = new
                {
                    scenario = "synthetic no-network RAG context + loopback fake Ollama",
                    warmup_requests = 5,
                    measured_requests_per_arm = 25,
                    baseline,
                    observed = enabled,
                    latency_p95_difference_ms = Math.Round(
                        enabled.LatencyP95Ms - baseline.LatencyP95Ms, 4),
                    process_cpu_difference_ms = Math.Round(
                        enabled.ProcessCpuMs - baseline.ProcessCpuMs, 4),
                    working_set_peak_difference_bytes =
                        enabled.WorkingSetPeakBytes - baseline.WorkingSetPeakBytes
                },
                limitations = new[]
                {
                    "The request uses an ASP.NET Core TestServer and isolated PostgreSQL Testcontainer, not production traffic.",
                    "Ollama is a deterministic loopback HTTP stub; native model inference is not benchmarked.",
                    "Process CPU, allocations and memory include collector/test-host activity and GC noise.",
                    "OTLP metrics are aggregates by operation/outcome; they intentionally lack trace_id labels.",
                    "No energy consumption or production latency gain is inferred from this sample."
                }
            };

            var jsonFile = Path.Combine(reportDirectory, "kh0054-observability.json");
            var mdFile = Path.Combine(reportDirectory, "kh0054-observability.md");
            await File.WriteAllTextAsync(jsonFile, JsonSerializer.Serialize(report, JsonOptions));
            await File.WriteAllTextAsync(mdFile, FormatMarkdown(baseline, enabled,
                collector.Count("/v1/traces"), collector.Count("/v1/metrics")));
            Console.WriteLine("KH0054_REPORT_JSON=" + jsonFile);
            Console.WriteLine("KH0054_REPORT_MARKDOWN=" + mdFile);
        }
        finally
        {
            await ((IAsyncLifetime)databaseHost).DisposeAsync();
        }
    }

    private static WebApplicationFactory<global::Program> CreateHost(
        CustomWebApplicationFactory parent,
        LoopbackCollector collector,
        bool enabled) => parent.WithWebHostBuilder(builder =>
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Tracing:Enabled"] = enabled.ToString(),
                ["Metrics:Enabled"] = enabled.ToString(),
                ["Tracing:SampleRatio"] = "1.0",
                ["Tracing:OtlpProtocol"] = "http/protobuf",
                ["Metrics:OtlpProtocol"] = "http/protobuf",
                ["Tracing:OtlpEndpoint"] = enabled
                    ? collector.Url + "v1/traces" : null,
                ["Metrics:OtlpEndpoint"] = enabled
                    ? collector.Url + "v1/metrics" : null,
                ["Metrics:ExportIntervalMilliseconds"] = "1000"
            }));
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IVectorSearchService>();
            services.AddScoped<IVectorSearchService, SyntheticVectorSearchService>();
            services.RemoveAll<IChatCompletionService>();
            services.AddHttpClient<IChatCompletionService, OllamaChatCompletionService>(client =>
            {
                client.BaseAddress = new Uri(collector.Url);
            });
        });
    });

    private static HttpClient CreateClient(WebApplicationFactory<global::Program> host) =>
        host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost/"),
            AllowAutoRedirect = false
        });

    private static async Task SendAsync(HttpClient client, string correlationId, bool traced)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/rag/ask")
        {
            Content = JsonContent.Create(new AskQuestionRequestDto { Question = Question })
        };
        request.Headers.TryAddWithoutValidation("X-Correlation-ID", correlationId);
        if (traced)
        {
            request.Headers.TryAddWithoutValidation("traceparent",
                "00-" + TraceId + "-" + ParentSpanId + "-01");
        }

        using var response = await client.SendAsync(request);
        Require(response.StatusCode == HttpStatusCode.OK, "RAG HTTP response is not 200");
        Require(response.Headers.TryGetValues("X-Correlation-ID", out var returned) &&
            returned.Single() == correlationId, "HTTP correlation header mismatch");
        var body = await response.Content.ReadAsStringAsync();
        Require(body.Contains(Answer, StringComparison.Ordinal), "fake Ollama response missing");
    }

    private static async Task<BenchResult> BenchmarkAsync(
        HttpClient client, string label, int warmup, int samples)
    {
        for (var i = 0; i < warmup; i++)
        {
            await SendAsync(client, "kh0054-warmup-" + i.ToString("D8"), false);
        }

        using var process = Process.GetCurrentProcess();
        process.Refresh();
        var cpuBefore = process.TotalProcessorTime.TotalMilliseconds;
        var allocatedBefore = GC.GetTotalAllocatedBytes(precise: false);
        var wsBefore = process.WorkingSet64;
        var peak = wsBefore;
        var latencies = new List<double>(samples);

        for (var i = 0; i < samples; i++)
        {
            var started = Stopwatch.GetTimestamp();
            await SendAsync(client, "kh0054-bench-" + label + "-" + i.ToString("D8"), false);
            latencies.Add(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            process.Refresh();
            peak = Math.Max(peak, process.WorkingSet64);
        }

        process.Refresh();
        latencies.Sort();
        var cpu = process.TotalProcessorTime.TotalMilliseconds - cpuBefore;
        var wsAfter = process.WorkingSet64;
        var allocated = GC.GetTotalAllocatedBytes(precise: false) - allocatedBefore;
        return new BenchResult(
            label,
            samples,
            Math.Round(latencies[latencies.Count / 2], 4),
            Math.Round(latencies[(int)Math.Ceiling(0.95 * latencies.Count) - 1], 4),
            Math.Round(cpu, 4),
            wsBefore,
            wsAfter,
            peak,
            allocated);
    }

    private static SpanRecord SingleSpan(IEnumerable<SpanRecord> spans, string name)
    {
        var matches = spans.Where(span => span.Name == name).ToArray();
        Require(matches.Length == 1, "missing or duplicate span: " + name);
        return matches[0];
    }

    private static bool FindSafeLogCorrelation(string correlationId, string traceId)
    {
        var logFolder = Path.Combine(Directory.GetCurrentDirectory(), "logs");
        if (!Directory.Exists(logFolder))
        {
            return false;
        }
        foreach (var path in Directory.EnumerateFiles(logFolder, "log-*.jsonl"))
        {
            foreach (var line in File.ReadLines(path))
            {
                if (!line.Contains(correlationId, StringComparison.Ordinal))
                {
                    continue;
                }
                if (Secrets.Any(secret => line.Contains(secret, StringComparison.Ordinal)))
                {
                    throw new VerificationException("private sentinel was written to logs");
                }
                using var doc = JsonDocument.Parse(line);
                if (!doc.RootElement.TryGetProperty("Properties", out var props))
                {
                    continue;
                }
                if (props.TryGetProperty("CorrelationId", out var correlation) &&
                    correlation.GetString() == correlationId &&
                    props.TryGetProperty("TraceId", out var trace) &&
                    string.Equals(trace.GetString(), traceId, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private static string FormatMarkdown(BenchResult baseline, BenchResult enabled,
        int traceRequests, int metricRequests) =>
        $"""
        # KH-005.4 — Synthetic observability validation

        - RAG HTTP request: 200, correlation log + W3C trace verified
        - OTLP/HTTP trace POSTs: {traceRequests}
        - OTLP/HTTP metric POSTs: {metricRequests}
        - Prompt/context/title/answer sentinels absent from telemetry and correlated log
        - Runs: 5 warmups and 25 measured calls per arm

        | Measurement | Disabled | Enabled |
        | --- | ---: | ---: |
        | HTTP p50 (ms) | {baseline.LatencyP50Ms} | {enabled.LatencyP50Ms} |
        | HTTP p95 (ms) | {baseline.LatencyP95Ms} | {enabled.LatencyP95Ms} |
        | Total process CPU (ms) | {baseline.ProcessCpuMs} | {enabled.ProcessCpuMs} |
        | Peak process working set (bytes) | {baseline.WorkingSetPeakBytes} | {enabled.WorkingSetPeakBytes} |
        | Process managed allocations (bytes) | {baseline.ProcessAllocatedBytes} | {enabled.ProcessAllocatedBytes} |

        **Interpretation:** synthetic TestServer + local fake Ollama + isolated PostgreSQL.
        Process-wide CPU/memory contain collector work and GC noise. The values are
        observations, not guaranteed performance gains or energy measurements.
        Metrics are aggregated by operation and cannot be joined one-to-one by trace ID.
        """;

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new VerificationException(message);
        }
    }

    private sealed class VerificationException : Exception
    {
        public VerificationException(string message) : base(message) { }
    }

    private sealed record BenchResult(
        string Variant,
        int Requests,
        double LatencyP50Ms,
        double LatencyP95Ms,
        double ProcessCpuMs,
        long WorkingSetBeforeBytes,
        long WorkingSetAfterBytes,
        long WorkingSetPeakBytes,
        long ProcessAllocatedBytes);

    private sealed class SyntheticVectorSearchService : IVectorSearchService
    {
        public SyntheticVectorSearchService() { }

        public Task<IReadOnlyCollection<VectorSearchResultDto>> SearchAsync(
            string question, int topK = 5, CancellationToken cancellationToken = default) =>
            KnowledgeHubMetrics.MeasureRetrievalAsync(() =>
                KnowledgeHubActivities.TraceAsync<IReadOnlyCollection<VectorSearchResultDto>>(
                    "vector_search.search",
                    () => Task.FromResult<IReadOnlyCollection<VectorSearchResultDto>>(
                    [new VectorSearchResultDto
                    {
                        ChunkId = Guid.Parse("a0000000-0000-0000-0000-000000000001"),
                        DocumentId = Guid.Parse("b0000000-0000-0000-0000-000000000001"),
                        Title = Title,
                        Content = Document,
                        Score = 0.9
                    }]),
                    cancellationToken), cancellationToken);
    }

    /// <summary>
    /// Process-scoped telemetry settings read by Program.cs before Build().
    /// The verifier runs in its own process; values are restored before exit.
    /// No user credentials or external URLs are introduced.
    /// </summary>
    private sealed class ScopedTelemetryEnvironment : IDisposable
    {
        private static readonly string[] Keys =
        [
            "Tracing__Enabled", "Metrics__Enabled", "Tracing__SampleRatio",
            "Tracing__OtlpProtocol", "Metrics__OtlpProtocol",
            "Tracing__OtlpEndpoint", "Metrics__OtlpEndpoint",
            "Metrics__ExportIntervalMilliseconds"
        ];

        private readonly Dictionary<string, string?> _prior = Keys.ToDictionary(
            key => key, Environment.GetEnvironmentVariable, StringComparer.Ordinal);

        public void SetForHost(string collectorUrl, bool enabled)
        {
            Environment.SetEnvironmentVariable("Tracing__Enabled", enabled ? "true" : "false");
            Environment.SetEnvironmentVariable("Metrics__Enabled", enabled ? "true" : "false");
            Environment.SetEnvironmentVariable("Tracing__SampleRatio", "1.0");
            Environment.SetEnvironmentVariable("Tracing__OtlpProtocol", "http/protobuf");
            Environment.SetEnvironmentVariable("Metrics__OtlpProtocol", "http/protobuf");
            Environment.SetEnvironmentVariable("Tracing__OtlpEndpoint",
                enabled ? collectorUrl + "v1/traces" : null);
            Environment.SetEnvironmentVariable("Metrics__OtlpEndpoint",
                enabled ? collectorUrl + "v1/metrics" : null);
            Environment.SetEnvironmentVariable("Metrics__ExportIntervalMilliseconds", "1000");
        }

        public void Dispose()
        {
            foreach (var item in _prior)
            {
                Environment.SetEnvironmentVariable(item.Key, item.Value);
            }
        }
    }

    private sealed record SpanRecord(
        string Name, string TraceId, string SpanId, string ParentSpanId);

    /// <summary>Minimal bounded protobuf wire reader for OTLP ResourceSpans/ScopeSpans/Span.</summary>
    private static class Proto
    {
        internal sealed record Field(int Number, byte[] Value);

        internal static IEnumerable<Field> Fields(byte[] bytes)
        {
            var offset = 0;
            while (offset < bytes.Length)
            {
                var tag = ReadVarint(bytes, ref offset);
                var number = checked((int)(tag >> 3));
                var wire = (int)(tag & 7);
                switch (wire)
                {
                    case 0:
                        ReadVarint(bytes, ref offset);
                        break;
                    case 1:
                        offset = checked(offset + 8);
                        break;
                    case 2:
                        var length = checked((int)ReadVarint(bytes, ref offset));
                        if (length < 0 || length > bytes.Length - offset)
                        {
                            throw new InvalidDataException("invalid protobuf length");
                        }
                        var value = new byte[length];
                        Array.Copy(bytes, offset, value, 0, length);
                        offset += length;
                        yield return new Field(number, value);
                        break;
                    case 5:
                        offset = checked(offset + 4);
                        break;
                    default:
                        throw new InvalidDataException("unsupported protobuf wire type");
                }
                if (offset > bytes.Length)
                {
                    throw new InvalidDataException("invalid protobuf boundary");
                }
            }
        }

        private static ulong ReadVarint(byte[] bytes, ref int offset)
        {
            ulong result = 0;
            for (var shift = 0; shift <= 63; shift += 7)
            {
                if (offset >= bytes.Length)
                {
                    throw new InvalidDataException("truncated protobuf field");
                }
                var value = bytes[offset++];
                result |= (ulong)(value & 127) << shift;
                if ((value & 128) == 0)
                {
                    return result;
                }
            }
            throw new InvalidDataException("protobuf varint too long");
        }

        internal static IEnumerable<SpanRecord> DecodeSpans(byte[] payload)
        {
            foreach (var root in Fields(payload).Where(field => field.Number == 1))
            foreach (var scope in Fields(root.Value).Where(field => field.Number == 2))
            foreach (var entry in Fields(scope.Value).Where(field => field.Number == 2))
            {
                var fields = Fields(entry.Value).ToArray();
                var name = fields.FirstOrDefault(field => field.Number == 5)?.Value;
                var trace = fields.FirstOrDefault(field => field.Number == 1)?.Value;
                var span = fields.FirstOrDefault(field => field.Number == 2)?.Value;
                var parent = fields.FirstOrDefault(field => field.Number == 4)?.Value;
                if (name is not null && trace is not null && span is not null)
                {
                    yield return new SpanRecord(Encoding.UTF8.GetString(name),
                        Convert.ToHexStringLower(trace), Convert.ToHexStringLower(span),
                        parent is null ? "" : Convert.ToHexStringLower(parent));
                }
            }
        }
    }

    private sealed class LoopbackCollector : IAsyncDisposable
    {
        private const int MaximumPayload = 2_000_000;
        private readonly HttpListener _listener = new();
        private readonly ConcurrentQueue<(string Path, byte[] Body)> _requests = new();
        private readonly ConcurrentQueue<string> _ollamaTraceparents = new();
        private readonly Task _worker;
        private readonly string _answer;
        public string Url { get; }

        public LoopbackCollector(string answer)
        {
            _answer = answer;
            var reserved = new TcpListener(IPAddress.Loopback, 0);
            reserved.Start();
            var port = ((IPEndPoint)reserved.LocalEndpoint).Port;
            reserved.Stop();
            Url = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/";
            _listener.Prefixes.Add(Url);
            _listener.Start();
            _worker = Task.Run(AcceptAsync);
        }

        private async Task AcceptAsync()
        {
            while (_listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = await _listener.GetContextAsync();
                }
                catch (HttpListenerException) when (!_listener.IsListening)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                await HandleAsync(context);
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            var path = context.Request.Url?.AbsolutePath ?? "";
            try
            {
                if (context.Request.ContentLength64 > MaximumPayload)
                {
                    context.Response.StatusCode = 413;
                    return;
                }
                using var stream = new MemoryStream();
                await context.Request.InputStream.CopyToAsync(stream);
                if (stream.Length > MaximumPayload)
                {
                    context.Response.StatusCode = 413;
                    return;
                }

                if (path is "/v1/traces" or "/v1/metrics")
                {
                    _requests.Enqueue((path, stream.ToArray()));
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/x-protobuf";
                    context.Response.ContentLength64 = 0;
                }
                else if (path == "/api/chat")
                {
                    _ollamaTraceparents.Enqueue(context.Request.Headers["traceparent"] ?? "");
                    var response = JsonSerializer.SerializeToUtf8Bytes(new
                    {
                        message = new { role = "assistant", content = _answer }
                    });
                    context.Response.StatusCode = 200;
                    context.Response.ContentType = "application/json";
                    context.Response.ContentLength64 = response.Length;
                    await context.Response.OutputStream.WriteAsync(response);
                }
                else
                {
                    context.Response.StatusCode = 404;
                }
            }
            finally
            {
                context.Response.Close();
            }
        }

        public int Count(string path) => _requests.Count(request => request.Path == path);

        public bool HasOutboundTraceparent(string traceId, string spanId) =>
            _ollamaTraceparents.Any(header =>
                header.StartsWith("00-" + traceId + "-" + spanId + "-",
                    StringComparison.OrdinalIgnoreCase));

        public bool ContainsAny(IEnumerable<string> secrets)
        {
            foreach (var payload in _requests)
            foreach (var secret in secrets)
            {
                if (payload.Body.AsSpan().IndexOf(Encoding.UTF8.GetBytes(secret)) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public async Task<(IReadOnlyList<SpanRecord> Spans, int Metrics)> WaitForTelemetryAsync(
            TimeSpan timeout, string traceId, params string[] requiredMetrics)
        {
            var deadline = Stopwatch.StartNew();
            while (deadline.Elapsed < timeout)
            {
                var spans = _requests.Where(x => x.Path == "/v1/traces")
                    .SelectMany(x => Proto.DecodeSpans(x.Body)).ToArray();
                var metrics = _requests.Where(x => x.Path == "/v1/metrics")
                    .Select(x => x.Body).ToArray();
                var relevant = spans.Where(span => span.TraceId == traceId).ToArray();
                if (relevant.Length >= 7 &&
                    requiredMetrics.All(metric => metrics.Any(payload =>
                        payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(metric)) >= 0)))
                {
                    return (spans, metrics.Length);
                }
                await Task.Delay(150);
            }
            // These are numeric/categorical diagnostics only; never emit raw
            // OTLP bodies, span tags, exception messages or document content.
            var finalSpans = _requests.Where(x => x.Path == "/v1/traces")
                .SelectMany(x => Proto.DecodeSpans(x.Body))
                .Count(span => span.TraceId == traceId);
            var finalMetrics = _requests.Where(x => x.Path == "/v1/metrics")
                .Select(x => x.Body).ToArray();
            var metricMatches = requiredMetrics.Count(metric => finalMetrics.Any(
                payload => payload.AsSpan().IndexOf(Encoding.UTF8.GetBytes(metric)) >= 0));
            throw new VerificationException(
                "OTLP incomplete: trace_posts=" + Count("/v1/traces") +
                ", metric_posts=" + Count("/v1/metrics") +
                ", matched_spans=" + finalSpans +
                ", required_metric_names=" + metricMatches + "/" + requiredMetrics.Length);
        }

        public async ValueTask DisposeAsync()
        {
            _listener.Stop();
            _listener.Close();
            await _worker;
        }
    }
}
