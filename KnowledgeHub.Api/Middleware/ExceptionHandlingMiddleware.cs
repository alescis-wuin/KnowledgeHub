using KnowledgeHub.Application.DTOs;
using System.Text.Json;

namespace KnowledgeHub.Api.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;

    public ExceptionHandlingMiddleware(
        RequestDelegate next)
    {
        _next = next;
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
            await HandleException(
                context,
                StatusCodes.Status400BadRequest,
                ex.Message);
        }
        catch (Exception ex)
        {
            await HandleException(
                context,
                StatusCodes.Status500InternalServerError,
                ex.Message);
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