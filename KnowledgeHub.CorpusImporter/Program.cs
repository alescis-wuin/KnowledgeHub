using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.CorpusImporter.Services;
using KnowledgeHub.Infrastructure.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

if (args.Length > 1)
{
    throw new ArgumentException("Usage: KnowledgeHub.CorpusImporter [corpus.json]");
}

var corpusPath = Path.GetFullPath(args.Length == 1
    ? args[0]
    : Path.Combine("Data", "KnowledgeHub-corpus-documentaire.json"));

var configuration = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false)
    .Build();

var services = new ServiceCollection();
services.AddInfrastructure(configuration);
services.AddScoped<ITextChunker, DefaultTextChunker>();
services.AddScoped<IChunkEmbeddingGenerator, ChunkEmbeddingGenerator>();
services.AddScoped<CorpusReader>();
services.AddScoped<CorpusImporter>();

await using var provider = services.BuildServiceProvider(validateScopes: true);
using var scope = provider.CreateScope();

var reader = scope.ServiceProvider.GetRequiredService<CorpusReader>();
var importer = scope.ServiceProvider.GetRequiredService<CorpusImporter>();

var corpus = await reader.ReadAsync(corpusPath);
Console.WriteLine($"Corpus: {corpusPath}");
Console.WriteLine($"Categories: {corpus.Categories.Count}");
Console.WriteLine($"Sources: {corpus.Sources.Count}");
Console.WriteLine($"Tags: {corpus.Tags.Count}");
Console.WriteLine($"Documents: {corpus.Documents.Count}");

await importer.ImportAsync(corpus);
Console.WriteLine("Importation et indexation terminees.");
