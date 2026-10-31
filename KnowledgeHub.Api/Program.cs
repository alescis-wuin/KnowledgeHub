using FluentValidation;
using FluentValidation.AspNetCore;
using KnowledgeHub.Api.Extensions;
using KnowledgeHub.Api.Middleware;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Validators;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;
using Serilog.Formatting.Json;
using KnowledgeHub.Infrastructure.DependencyInjection;

// Existing Serilog sinks; JSONL is consumed by a collector without scraping text.
// The default log context enriches downstream ILogger<T> events with CorrelationId.
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .WriteTo.File(
        new JsonFormatter(renderMessage: true),
        "logs/log-.jsonl",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7)
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllers();

builder.Services.AddFluentValidationAutoValidation();

builder.Services.AddValidatorsFromAssemblyContaining<
    CreateDocumentDtoValidator>();
builder.Services.AddValidatorsFromAssemblyContaining<
    UpdateDocumentDtoValidator>();

builder.Services.AddOpenApi();

builder.Services.AddInfrastructure(
    builder.Configuration);

builder.Services.AddApplicationServices();

builder.Services.AddKnowledgeHubTracing(builder.Configuration);
builder.Services.AddKnowledgeHubMetrics(builder.Configuration);

builder.Services.Configure<ApiBehaviorOptions>(
    options =>
    {
        options.InvalidModelStateResponseFactory =
            context =>
            {
                var errors =
                    context.ModelState
                        .Values
                        .SelectMany(v => v.Errors)
                        .Select(e => e.ErrorMessage)
                        .ToArray();

                return new BadRequestObjectResult(
                    ApiResponses.Failure<object>(
                        StatusCodes.Status400BadRequest,
                        context.HttpContext.TraceIdentifier,
                        errors));
            };
    });

var app = builder.Build();
// Outer middleware sees the final status emitted by the exception handler.
app.UseMiddleware<RequestCorrelationLoggingMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "KnowledgeHub API";
    });
}

app.UseHttpsRedirection();

app.MapControllers();

//
// Vérification connexion BDD
//
using (var scope = app.Services.CreateScope())
{
    var context =
        scope.ServiceProvider.GetRequiredService<
            KnowledgeDbContext>();

    try
    {
        var canConnect =
            await context.Database.CanConnectAsync();

        if (canConnect)
        {
            app.Logger.LogInformation(
                "Database connectivity probe succeeded");
        }
        else
        {
            app.Logger.LogWarning(
                "Database connectivity probe reported an unavailable database");
        }
    }
    catch (Exception ex)
    {
        // Do not print connection strings or exception messages.
        app.Logger.LogWarning(
            "Database connectivity probe failed: {ExceptionType}",
            ex.GetType().Name);
    }
}

app.Run();

public partial class Program
{
}