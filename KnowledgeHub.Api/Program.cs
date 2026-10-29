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
using KnowledgeHub.Infrastructure.DependencyInjection;

Log.Logger =
    new LoggerConfiguration()
        .WriteTo.Console()
        .WriteTo.File(
            "logs/log-.txt",
            rollingInterval:
                RollingInterval.Day)
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
app.UseMiddleware<
    ExceptionHandlingMiddleware>();

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

        Console.WriteLine(
            $"PostgreSQL connection : {canConnect}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(ex.Message);
    }
}

app.Run();

public partial class Program
{
}