using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Evaluation;

public sealed class EvaluationDatasetImportService
    : IEvaluationDatasetImportService
{
    private readonly KnowledgeDbContext _dbContext;

    private readonly IEnumerable<IEvaluationDatasetParser> _parsers;

    public EvaluationDatasetImportService(
        KnowledgeDbContext dbContext,
        IEnumerable<IEvaluationDatasetParser> parsers)
    {
        _dbContext = dbContext;
        _parsers = parsers;
    }

    public async Task ImportAllAsync(
        CancellationToken cancellationToken)
    {
        var rootFolder =
            Path.Combine(
                AppContext.BaseDirectory,
                "Data",
                "benchmarks");

        if (!Directory.Exists(rootFolder))
        {
            Console.WriteLine(
                $"Benchmark root not found: {rootFolder}");

            return;
        }

        Console.WriteLine(
            "Suppression des anciens datasets d'évaluation...");

        await ClearBenchmarkDataAsync(
            cancellationToken);

        foreach (var parser in _parsers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var datasetFolder =
                FindDatasetFolder(
                    rootFolder,
                    parser.DatasetType);

            if (datasetFolder is null)
            {
                Console.WriteLine(
                    $"Dataset introuvable pour {parser.DatasetType}");

                continue;
            }

            Console.WriteLine(
                $"Import de {parser.DatasetType}...");

            var dataset =
                await parser.ParseAsync(
                    datasetFolder,
                    cancellationToken);

            _dbContext.EvaluationDatasets
                .Add(dataset);

            await _dbContext.SaveChangesAsync(
                cancellationToken);

            Console.WriteLine(
                $"{parser.DatasetType} importé.");
        }
    }

    private async Task ClearBenchmarkDataAsync(
        CancellationToken cancellationToken)
    {
        await _dbContext
            .EvaluationReferenceDocuments
            .ExecuteDeleteAsync(
                cancellationToken);

        await _dbContext
            .EvaluationQuestions
            .ExecuteDeleteAsync(
                cancellationToken);

        await _dbContext
            .EvaluationDatasets
            .ExecuteDeleteAsync(
                cancellationToken);
    }

    private static string? FindDatasetFolder(
        string rootFolder,
        string datasetType)
    {
        return datasetType.ToUpperInvariant() switch
        {
            "FIQA" =>
                Path.Combine(
                    rootFolder,
                    "fiqa"),

            "SCIFACT" =>
                Path.Combine(
                    rootFolder,
                    "scifact"),

            "HOTPOTQA" =>
                Path.Combine(
                    rootFolder,
                    "hotpotqa"),

            _ => null
        };
    }
}