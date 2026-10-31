using FluentAssertions;
using KnowledgeHub.Api.Middleware;
using KnowledgeHub.Application.DTOs;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace KnowledgeHub.Tests.Unit.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    [Fact]
    public async Task Should_Call_Next_When_No_Exception()
    {
        var context =
            new DefaultHttpContext();

        var called = false;

        RequestDelegate next = _ =>
        {
            called = true;
            return Task.CompletedTask;
        };

        var middleware =
            new ExceptionHandlingMiddleware(
                next);

        await middleware.InvokeAsync(
            context);

        called.Should().BeTrue();
    }

    [Fact]
    public async Task Should_Return_400_For_ArgumentException()
    {
        var context =
            new DefaultHttpContext();

        context.Response.Body =
            new MemoryStream();

        RequestDelegate next =
            _ => throw new ArgumentException(
                "Erreur métier");

        var middleware =
            new ExceptionHandlingMiddleware(
                next);

        await middleware.InvokeAsync(
            context);

        context.Response.StatusCode
            .Should()
            .Be(StatusCodes.Status400BadRequest);

        var body =
            await GetBodyAsync(
                context.Response);

        var response =
            JsonSerializer.Deserialize<
                ApiResponse<object>>(body);

        response.Should()
            .NotBeNull();

        response!.Success
            .Should()
            .BeFalse();

        response.StatusCode
            .Should()
            .Be(400);

        response.Errors
            .Should()
            .ContainSingle();

        response.Errors.First()
            .Should()
            .Be("Erreur métier");
    }

    [Fact]
    public async Task Should_Return_500_For_Exception()
    {
        var context =
            new DefaultHttpContext();

        context.Response.Body =
            new MemoryStream();

        RequestDelegate next =
            _ => throw new Exception(
                "Erreur technique");

        var middleware =
            new ExceptionHandlingMiddleware(
                next);

        await middleware.InvokeAsync(
            context);

        context.Response.StatusCode
            .Should()
            .Be(StatusCodes.Status500InternalServerError);

        var body =
            await GetBodyAsync(
                context.Response);

        var response =
                    JsonSerializer.Deserialize<
                        ApiResponse<object>>(body);

        response.Should()
            .NotBeNull();

        response!.Success
            .Should()
            .BeFalse();

        response.StatusCode
            .Should()
            .Be(500);

        response.Errors
            .Should()
            .ContainSingle();

        response.Errors.First()
            .Should()
            .Be("Une erreur interne est survenue.");
    }

    [Fact]
    public async Task ServerError_DoesNotExposeExceptionMessageToLogOrClient()
    {
        const string secret = "CONNECTION_PASSWORD_AND_PRIVATE_DOCUMENT";
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var logger = new RecordingLogger();
        var middleware = new ExceptionHandlingMiddleware(
            _ => throw new InvalidOperationException(secret), logger);

        await middleware.InvokeAsync(context);

        var logged = Assert.Single(logger.Entries);
        Assert.Equal(51003, logged.EventId.Id);
        Assert.DoesNotContain(secret, logged.Message);
        Assert.DoesNotContain(secret, await GetBodyAsync(context.Response));
    }

    private sealed class RecordingLogger
        : Microsoft.Extensions.Logging.ILogger<ExceptionHandlingMiddleware>
    {
        public List<(Microsoft.Extensions.Logging.EventId EventId, string Message)> Entries { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull =>
            NoopScope.Instance;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel level) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel level,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((eventId, formatter(state, exception)));

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }

    private static async Task<string> GetBodyAsync(
        HttpResponse response)
    {
        response.Body.Seek(
            0,
            SeekOrigin.Begin);

        using var reader =
            new StreamReader(
                response.Body);

        return await reader.ReadToEndAsync();
    }
}