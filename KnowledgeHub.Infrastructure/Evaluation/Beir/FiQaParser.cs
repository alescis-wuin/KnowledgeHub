using System.Text.Json;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Models.Evaluation;
using KnowledgeHub.Infrastructure.Evaluation.Beir.Models;

namespace KnowledgeHub.Infrastructure.Evaluation.Beir;

public sealed class FiQaParser
    : IEvaluationDatasetParser
{
    public string DatasetType => "fiqa";

    public async Task<EvaluationDatasetImportModel> ParseAsync(
        string datasetPath,
        CancellationToken cancellationToken)
    {
        var corpusPath =
            Path.Combine(
                datasetPath,
                "corpus.jsonl");

        var queriesPath =
            Path.Combine(
                datasetPath,
                "queries.jsonl");

        var trainQrels =
            Path.Combine(
                datasetPath,
                "qrels",
                "train.tsv");

        var corpus =
            await LoadCorpusAsync(
                corpusPath,
                cancellationToken);

        var queries =
            await LoadQueriesAsync(
                queriesPath,
                cancellationToken);

        var qrels =
            await LoadQrelsAsync(
                trainQrels,
                cancellationToken);

        var dataset =
            new EvaluationDatasetImportModel
            {
                DatasetName = "FIQA",
                SourceType = "BEIR"
            };

        foreach (var query in queries.Values)
        {
            var question =
                new EvaluationQuestionImportModel
                {
                    ExternalQuestionId = query.Id,
                    Question = query.Text,
                    Difficulty = "unknown"
                };

            foreach (var qrel in qrels.Where(
                         x => x.QueryId == query.Id))
            {
                if (!corpus.TryGetValue(
                        qrel.CorpusId,
                        out var document))
                {
                    continue;
                }

                question.Documents.Add(
                    new EvaluationReferenceDocumentImportModel
                    {
                        ExternalDocumentId =
                            document.Id,

                        Title =
                            document.Title,

                        Content =
                            document.Text
                    });
            }

            dataset.Questions.Add(
                question);
        }

        return dataset;
    }

    private static async Task<
        Dictionary<string, FiQaCorpusEntry>>
        LoadCorpusAsync(
            string path,
            CancellationToken cancellationToken)
    {
        var result =
            new Dictionary<
                string,
                FiQaCorpusEntry>();

        foreach (var line in await File.ReadAllLinesAsync(
                     path,
                     cancellationToken))
        {
            var document =
                JsonSerializer.Deserialize<
                    FiQaCorpusEntry>(line);

            if (document is null)
            {
                continue;
            }

            result[document.Id] =
                document;
        }

        return result;
    }

    private static async Task<
        Dictionary<string, FiQaQueryEntry>>
        LoadQueriesAsync(
            string path,
            CancellationToken cancellationToken)
    {
        var result =
            new Dictionary<
                string,
                FiQaQueryEntry>();

        foreach (var line in await File.ReadAllLinesAsync(
                     path,
                     cancellationToken))
        {
            var query =
                JsonSerializer.Deserialize<
                    FiQaQueryEntry>(line);

            if (query is null)
            {
                continue;
            }

            result[query.Id] =
                query;
        }

        return result;
    }

    private static async Task<
        List<FiQaQrelEntry>>
        LoadQrelsAsync(
            string path,
            CancellationToken cancellationToken)
    {
        var lines =
            await File.ReadAllLinesAsync(
                path,
                cancellationToken);

        var result =
            new List<FiQaQrelEntry>();

        foreach (var line in lines.Skip(1))
        {
            var parts =
                line.Split('\t');

            if (parts.Length < 3)
            {
                continue;
            }

            result.Add(
                new FiQaQrelEntry
                {
                    QueryId = parts[0],
                    CorpusId = parts[1],
                    Score = int.Parse(parts[2])
                });
        }

        return result;
    }
}