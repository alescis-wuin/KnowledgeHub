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
            .Be("Erreur technique");
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