using KnowledgeHub.Api.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace KnowledgeHub.Tests.Unit.Middleware;

public sealed class RequestCorrelationLoggingMiddlewareTests
{
    [Fact]
    public async Task ValidCorrelation_IsScopedAndDoesNotLogRawUrlOrPayload()
    {
        const string id = "kh-005-req-ABC_12345";
        const string secret = "PRIVATE_DOCUMENT_PAYLOAD_309812";
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Path = "/private/" + secret;
        context.Request.QueryString = new QueryString("?question=" + secret);
        context.Request.Headers[RequestCorrelationLoggingMiddleware.CorrelationHeader] = id;
        context.TraceIdentifier = "fallback-trace-id";
        using var body = new MemoryStream();
        context.Response.Body = body;

        var logger = new RecordingLogger();
        var middleware = new RequestCorrelationLoggingMiddleware(
            async http =>
            {
                Assert.Equal(id, http.TraceIdentifier);
                await http.Response.WriteAsync(secret);
            },
            logger);

        await middleware.InvokeAsync(context);

        var completed = Assert.Single(logger.Entries);
        Assert.Equal(51001, completed.EventId.Id);
        Assert.Equal("HttpRequestCompleted", completed.EventId.Name);
        Assert.DoesNotContain(secret, completed.Message);
        Assert.DoesNotContain("question", completed.Message);
        Assert.Contains("unmatched", completed.Message);
        Assert.Contains(logger.Scopes, x =>
            x is IEnumerable<KeyValuePair<string, object>> entries &&
            entries.Any(pair => pair.Key == "CorrelationId" &&
                                Equals(pair.Value, id)));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("../../invalid")]
    [InlineData("bad-*-chars")]
    [InlineData("weird:colon:in:header")]
    public async Task UnsafeCorrelation_IsNotTrusted(string supplied)
    {
        var context = new DefaultHttpContext();
        context.TraceIdentifier = "server-id-123456";
        context.Request.Headers[RequestCorrelationLoggingMiddleware.CorrelationHeader] = supplied;
        var logger = new RecordingLogger();
        var middleware = new RequestCorrelationLoggingMiddleware(
            http =>
            {
                Assert.Equal("server-id-123456", http.TraceIdentifier);
                return Task.CompletedTask;
            },
            logger);

        await middleware.InvokeAsync(context);
        Assert.Single(logger.Entries);
        Assert.DoesNotContain(supplied, logger.Entries[0].Message);
    }

    private sealed class RecordingLogger : ILogger<RequestCorrelationLoggingMiddleware>
    {
        public List<(EventId EventId, string Message)> Entries { get; } = [];
        public List<object> Scopes { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull
        {
            Scopes.Add(state);
            return NoopScope.Instance;
        }

        public bool IsEnabled(LogLevel level) => true;

        public void Log<TState>(LogLevel level, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Entries.Add((eventId, formatter(state, exception)));
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
