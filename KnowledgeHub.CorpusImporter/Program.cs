using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.CorpusImporter.Services;
using KnowledgeHub.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

var configuration =
    new ConfigurationBuilder()
        .SetBasePath(
            AppContext.BaseDirectory)
        .AddJsonFile(
            "appsettings.json",
            optional: false)
        .Build();

var services =
    new ServiceCollection();

services.AddInfrastructure(
    configuration);

services.AddScoped<
    ITextChunker,
    DefaultTextChunker>();

services.AddScoped<
    CorpusReader>();

services.AddScoped<
    CorpusImporter>();

services.AddScoped<
    EmbeddingGenerationService>();

var provider =
    services.BuildServiceProvider();

using var scope =
    provider.CreateScope();

var reader =
    scope.ServiceProvider
        .GetRequiredService<
            CorpusReader>();

var importer =
    scope.ServiceProvider
        .GetRequiredService<
            CorpusImporter>();

var corpus =
    await reader.ReadAsync(
        @"Data\KnowledgeHub-corpus-documentaire.json");

Console.WriteLine(
    $"Categories : {corpus.Categories.Count}");

Console.WriteLine(
    $"Sources : {corpus.Sources.Count}");

Console.WriteLine(
    $"Tags : {corpus.Tags.Count}");

Console.WriteLine(
    $"Documents : {corpus.Documents.Count}");

await importer.ImportAsync(
    corpus);

Console.WriteLine(
    "Import terminé.");

var embeddingGenerator =
    scope.ServiceProvider
        .GetRequiredService<
            EmbeddingGenerationService>();

await embeddingGenerator.GenerateAsync();

Console.WriteLine(
    "Vectorisation terminée.");
