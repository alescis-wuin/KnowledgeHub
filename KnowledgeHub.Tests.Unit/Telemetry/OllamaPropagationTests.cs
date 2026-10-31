using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using KnowledgeHub.Application.Telemetry;
using KnowledgeHub.Infrastructure.AI;
using OpenTelemetry;
using OpenTelemetry.Instrumentation.Http;
using OpenTelemetry.Trace;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class OllamaPropagationTests
{
    [Fact]
    public async Task HttpClient_PropagatesW3cTraceparentWithoutPromptsInTraceTags()
    {
        const string question = "PRIVATE_QUESTION_PROPAGATION";
        const string context = "PRIVATE_DOCUMENT_PROPAGATION";
        var activities = new ConcurrentBag<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == KnowledgeHubActivities.SourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(activity)
        };
        ActivitySource.AddActivityListener(listener);

        using var provider = Sdk.CreateTracerProviderBuilder()
            .AddSource(KnowledgeHubActivities.SourceName)
            .AddHttpClientInstrumentation(options => options.RecordException = false)
            .Build();

        // A stub HttpMessageHandler bypasses .NET's SocketsHttpHandler tracing.
        // Exercise the real transport against a local, single-request TCP server.
        var server = new TcpListener(IPAddress.Loopback, 0);
        server.Start();
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var serverTask = ServeOneRequestAsync(server, timeout.Token);
            var port = ((IPEndPoint)server.LocalEndpoint).Port;
            using var client = new HttpClient(new SocketsHttpHandler { UseProxy = false })
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}"),
                Timeout = TimeSpan.FromSeconds(15)
            };
            var service = new OllamaChatCompletionService(client);

            var parentContext = new ActivityContext(
                ActivityTraceId.CreateRandom(), ActivitySpanId.CreateRandom(),
                ActivityTraceFlags.Recorded);
            using var root = KnowledgeHubActivities.Source.StartActivity(
                "kh0052.test.propagation", ActivityKind.Internal, parentContext);
            Assert.NotNull(root);

            var answer = await service.GenerateAnswerAsync(question, context);
            var traceparent = await serverTask;
            Assert.Equal("documented", answer);
            Assert.NotNull(traceparent);
            Assert.StartsWith("00-" + root.TraceId.ToHexString() + "-", traceparent);

            var llm = Assert.Single(activities, x =>
                x.TraceId == root.TraceId && x.DisplayName == "ollama.chat");
            Assert.Equal(root.SpanId, llm.ParentSpanId);
            var snapshot = llm.DisplayName + string.Join(" ",
                llm.TagObjects.Select(tag => $"{tag.Key}={tag.Value}"));
            Assert.DoesNotContain(question, snapshot, StringComparison.Ordinal);
            Assert.DoesNotContain(context, snapshot, StringComparison.Ordinal);
        }
        finally
        {
            server.Stop();
        }
    }

    private static async Task<string?> ServeOneRequestAsync(
        TcpListener server, CancellationToken cancellationToken)
    {
        using var connection = await server.AcceptTcpClientAsync(cancellationToken);
        await using var stream = connection.GetStream();
        using var headers = new MemoryStream();
        var buffer = new byte[2048];
        while (headers.Length < 32768)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (count == 0)
            {
                break;
            }
            headers.Write(buffer, 0, count);
            var rawHeaders = Encoding.ASCII.GetString(headers.ToArray());
            var endOfHeaders = rawHeaders.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (endOfHeaders < 0)
            {
                continue;
            }

            var traceHeader = rawHeaders[..endOfHeaders]
                .Split("\r\n", StringSplitOptions.None)
                .FirstOrDefault(line => line.StartsWith(
                    "traceparent:", StringComparison.OrdinalIgnoreCase));
            var traceparent = traceHeader is null
                ? null
                : traceHeader["traceparent:".Length..].Trim();

            const string json = "{\"message\":{\"content\":\"documented\"}}";
            var body = Encoding.UTF8.GetBytes(json);
            var responseHeaders = Encoding.ASCII.GetBytes(
                $"HTTP/1.1 200 OK\r\nContent-Type: application/json\r\n" +
                $"Content-Length: {body.Length}\r\nConnection: close\r\n\r\n");
            await stream.WriteAsync(responseHeaders.AsMemory(), cancellationToken);
            await stream.WriteAsync(body.AsMemory(), cancellationToken);
            await stream.FlushAsync(cancellationToken);
            return traceparent;
        }

        throw new InvalidDataException("Local test server received no complete HTTP headers.");
    }
}
