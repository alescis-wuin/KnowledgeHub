using KnowledgeHub.Application.DTOs;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;

namespace KnowledgeHub.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private static readonly EventId RejectedEvent =
        new(51002, "HttpRequestRejected");
    private static readonly EventId FailureEvent =
        new(51003, "HttpRequestFailed");

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware>? logger = null)
    {
        _next = next;
        _logger = logger ?? NullLogger<ExceptionHandlingMiddleware>.Instance;
    }

    public async Task InvokeAsync(
        HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(RejectedEvent,
                "Request rejected: {ExceptionType}", ex.GetType().Name);
            await HandleException(
                context,
                StatusCodes.Status400BadRequest,
                ex.Message);
        }
        catch (Exception ex)
        {
            // Error text may contain the corpus or connection credentials.
            // Keep it out of both the server log and the public 500 response.
            _logger.LogError(FailureEvent,
                "Unexpected request failure: {ExceptionType}", ex.GetType().Name);
            await HandleException(
                context,
                StatusCodes.Status500InternalServerError,
                "Une erreur interne est survenue.");
        }
    }

    private static async Task HandleException(
        HttpContext context,
        int statusCode,
        string error)
    {
        context.Response.StatusCode = statusCode;

        context.Response.ContentType =
            "application/json";

        var response =
            ApiResponses.Failure<object>(
                statusCode,
                context.TraceIdentifier,
                error);

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response));
    }
}