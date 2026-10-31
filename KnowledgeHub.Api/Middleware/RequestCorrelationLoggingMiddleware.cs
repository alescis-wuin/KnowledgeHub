using System.Diagnostics;
using Microsoft.AspNetCore.Routing;

namespace KnowledgeHub.Api.Middleware;

/// <summary>
/// Emits one structured event per request. The route template is logged instead
/// of the raw URL: never capture query strings, headers, bodies or documents.
/// </summary>
public sealed class RequestCorrelationLoggingMiddleware
{
    public const string CorrelationHeader = "X-Correlation-ID";

    private static readonly EventId CompletedEvent =
        new(51001, "HttpRequestCompleted");

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationLoggingMiddleware> _logger;

    public RequestCorrelationLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // An incoming value is untrusted. Reject CR/LF, Unicode controls,
        // large inputs and structured-log injection instead of truncating.
        var correlationId = context.TraceIdentifier;
        if (context.Request.Headers.TryGetValue(CorrelationHeader, out var values) &&
            values.Count == 1 && IsSafeCorrelationId(values[0]))
        {
            correlationId = values[0]!;
        }

        context.TraceIdentifier = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationHeader] = correlationId;
            return Task.CompletedTask;
        });

        using var scope = _logger.BeginScope(
            new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            });

        var start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            // Route templates cannot contain the caller's raw query or document ID.
            var route = (context.GetEndpoint() as RouteEndpoint)?
                .RoutePattern.RawText ?? "unmatched";
            var durationMs = Math.Round(
                Stopwatch.GetElapsedTime(start).TotalMilliseconds, 2);
            var status = context.Response.StatusCode;

            _logger.LogInformation(
                CompletedEvent,
                "HTTP {HttpMethod} {RoutePattern} returned {StatusCode} in {ElapsedMs} ms",
                context.Request.Method,
                route,
                status,
                durationMs);
        }
    }

    private static bool IsSafeCorrelationId(string? value)
    {
        if (value is null || value.Length is < 8 or > 64)
        {
            return false;
        }

        foreach (var c in value)
        {
            if (!((c >= 'a' && c <= 'z') ||
                  (c >= 'A' && c <= 'Z') ||
                  (c >= '0' && c <= '9') ||
                  c is '-' or '_'))
            {
                return false;
            }
        }

        return true;
    }
}
