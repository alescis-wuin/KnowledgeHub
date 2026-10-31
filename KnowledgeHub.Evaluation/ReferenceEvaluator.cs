using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace KnowledgeHub.Evaluation;

/// <summary>
/// An offline, deterministic, domain-neutral benchmark of retrieved documents,
/// explicitly cited passages and abstentions. It does not judge semantic answers.
/// </summary>
public static class ReferenceEvaluator
{
    public const int TopK = 5;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static Baseline Load(string directory)
    {
        var manifest = Read<ReferenceManifest>(Path.Combine(directory, "manifest.json"));
        Require(manifest.SchemaVersion == 1 && manifest.CorpusId == "kh007-synthetic-v1",
            "Unexpected baseline identity or version.");
        Require(manifest.Files is not null && manifest.Files.Count == 3,
            "Unexpected baseline inventory.");
        foreach (var name in new[] { "corpus.json", "cases.json", "oracle-predictions.json" })
        {
            Require(manifest.Files.TryGetValue(name, out var expected) &&
                expected is { Length: 64 } && expected.All(Uri.IsHexDigit),
                "Baseline inventory is incomplete.");
            var actual = Convert.ToHexString(SHA256.HashData(
                File.ReadAllBytes(Path.Combine(directory, name))));
            Require(string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase),
                "Baseline fingerprint mismatch: " + name);
        }

        var corpus = Read<Corpus>(Path.Combine(directory, "corpus.json"));
        var cases = Read<GoldSet>(Path.Combine(directory, "cases.json"));
        var oracle = Read<PredictionSet>(Path.Combine(directory, "oracle-predictions.json"));
        ValidateGold(corpus, cases, manifest.CorpusId);
        ValidatePredictions(oracle, cases, corpus, manifest.CorpusId);
        return new Baseline(manifest, corpus, cases, oracle);
    }

    public static PredictionSet LoadPredictions(string path) => Read<PredictionSet>(path);

    public static EvaluationReport Evaluate(Baseline baseline, PredictionSet predictions)
    {
        var corpus = baseline.Corpus;
        var cases = baseline.Cases;
        ValidatePredictions(predictions, cases, corpus, baseline.Manifest.CorpusId);
        var byId = predictions.Predictions.ToDictionary(x => x.Id, StringComparer.Ordinal);
        var output = new List<CaseResult>(cases.Cases.Count);
        var documents = corpus.Documents.ToDictionary(x => x.Id);
        var answerable = 0;
        var abstention = 0;
        var correctAbstentions = 0;
        var retrievalPrecision = 0.0;
        var retrievalRecall = 0.0;
        var mrr = 0.0;
        var ndcg = 0.0;
        var evidenceMatched = 0;
        var evidencePredicted = 0;
        var evidenceRequired = 0;
        var verdictCorrect = 0;
        var supported = 0;

        foreach (var gold in cases.Cases.OrderBy(x => x.Id, StringComparer.Ordinal))
        {
            var prediction = byId[gold.Id];
            var correct = prediction.Verdict == gold.ExpectedVerdict;
            if (correct) verdictCorrect++;
            if (gold.ExpectedVerdict == "abstain")
            {
                abstention++;
                if (prediction.Verdict == "abstain") correctAbstentions++;
            }

            double precision = 0, recall = 0, reciprocalRank = 0, normalizedDcg = 0;
            var relevance = gold.RelevantDocuments.ToHashSet();
            if (gold.ExpectedVerdict == "answer")
            {
                answerable++;
                var ranked = prediction.RankedDocuments.Take(TopK).ToArray();
                var hits = ranked.Count(relevance.Contains);
                precision = (double)hits / TopK;
                recall = (double)hits / relevance.Count;
                var firstRelevant = Array.FindIndex(ranked, relevance.Contains);
                reciprocalRank = firstRelevant < 0 ? 0 : 1.0 / (firstRelevant + 1);
                var dcg = 0.0;
                for (var i = 0; i < ranked.Length; i++)
                {
                    if (relevance.Contains(ranked[i])) dcg += 1.0 / Math.Log2(i + 2);
                }
                var ideal = Enumerable.Range(0, Math.Min(TopK, relevance.Count))
                    .Sum(i => 1.0 / Math.Log2(i + 2));
                normalizedDcg = ideal == 0 ? 0 : dcg / ideal;
                retrievalPrecision += precision;
                retrievalRecall += recall;
                mrr += reciprocalRank;
                ndcg += normalizedDcg;
            }

            var expectedQuotes = gold.GoldEvidence
                .Select(x => new QuoteKey(x.DocumentId, x.Quote))
                .ToHashSet();
            var accepted = prediction.Evidence.Count(x =>
                documents[x.DocumentId].Content.Contains(x.Quote, StringComparison.Ordinal) &&
                expectedQuotes.Contains(new QuoteKey(x.DocumentId, x.Quote)));
            var quotePrecision = prediction.Evidence.Count == 0 ?
                (expectedQuotes.Count == 0 ? 1.0 : 0.0) :
                (double)accepted / prediction.Evidence.Count;
            var quoteRecall = expectedQuotes.Count == 0 ?
                (prediction.Evidence.Count == 0 ? 1.0 : 0.0) :
                (double)accepted / expectedQuotes.Count;
            var grounded = correct &&
                (gold.ExpectedVerdict == "abstain" ? prediction.Evidence.Count == 0 :
                    accepted == expectedQuotes.Count &&
                    accepted == prediction.Evidence.Count &&
                    gold.RelevantDocuments.All(id => prediction.RankedDocuments.Take(TopK).Contains(id)));
            if (grounded) supported++;
            evidenceMatched += accepted;
            evidencePredicted += prediction.Evidence.Count;
            evidenceRequired += expectedQuotes.Count;
            output.Add(new CaseResult(gold.Id, gold.ExpectedVerdict, prediction.Verdict, correct,
                Math.Round(precision, 6), Math.Round(recall, 6),
                Math.Round(reciprocalRank, 6), Math.Round(normalizedDcg, 6),
                accepted, expectedQuotes.Count, prediction.Evidence.Count,
                Math.Round(quotePrecision, 6), Math.Round(quoteRecall, 6), grounded));
        }

        Require(answerable > 0 && abstention > 0, "Both answerable and abstention cases are required.");
        return new EvaluationReport(1, baseline.Manifest.CorpusId, TopK,
            corpus.Documents.Count, cases.Cases.Count, answerable, abstention,
            Math.Round(retrievalPrecision / answerable, 6),
            Math.Round(retrievalRecall / answerable, 6),
            Math.Round(mrr / answerable, 6), Math.Round(ndcg / answerable, 6),
            Math.Round((double)correctAbstentions / abstention, 6),
            Math.Round((double)verdictCorrect / cases.Cases.Count, 6),
            Math.Round(evidencePredicted == 0 ? (evidenceRequired == 0 ? 1 : 0) :
                (double)evidenceMatched / evidencePredicted, 6),
            Math.Round(evidenceRequired == 0 ? (evidencePredicted == 0 ? 1 : 0) :
                (double)evidenceMatched / evidenceRequired, 6),
            Math.Round((double)supported / cases.Cases.Count, 6), output);
    }

    public static void SelfTest(Baseline baseline)
    {
        Require(baseline.Corpus.Documents.Count >= 10 && baseline.Cases.Cases.Count >= 15,
            "Reference is too small for the required coverage.");
        var perfect = Evaluate(baseline, baseline.Oracle);
        Require(perfect.RecallAtK == 1 && perfect.MrrAtK == 1 && perfect.NdcgAtK == 1 &&
            perfect.AbstentionAccuracy == 1 && perfect.VerdictAccuracy == 1 &&
            perfect.EvidencePrecision == 1 && perfect.EvidenceRecall == 1 &&
            perfect.GroundedVerdictRate == 1, "Oracle fixture must meet all gold checks.");

        PredictionSet Clone() => JsonSerializer.Deserialize<PredictionSet>(
            JsonSerializer.Serialize(baseline.Oracle, Json), Json) ??
            throw new InvalidOperationException("Fixture clone failed.");
        var first = baseline.Cases.Cases.First(c => c.ExpectedVerdict == "answer").Id;
        var lostRetrieval = Clone();
        lostRetrieval.Predictions.Single(p => p.Id == first).RankedDocuments.Clear();
        Require(Evaluate(baseline, lostRetrieval).RecallAtK < 1,
            "Missing retrieved documents must reduce recall.");

        var unsupportedQuote = Clone();
        unsupportedQuote.Predictions.Single(p => p.Id == first).Evidence[0].Quote =
            "UNSUPPORTED_EVIDENCE_SENTINEL";
        Require(Evaluate(baseline, unsupportedQuote).GroundedVerdictRate < 1,
            "Unsupported quotes must reduce grounded verdict rate.");

        var falseAnswer = Clone();
        falseAnswer.Predictions.First(p => p.Verdict == "abstain").Verdict = "answer";
        Require(Evaluate(baseline, falseAnswer).AbstentionAccuracy < 1,
            "False answers must reduce abstention accuracy.");

        var unknownDocument = Clone();
        unknownDocument.Predictions.Single(p => p.Id == first).RankedDocuments[0] = Guid.NewGuid();
        ExpectRejected(() => Evaluate(baseline, unknownDocument));
        var duplicate = Clone();
        duplicate.Predictions.Add(duplicate.Predictions[0]);
        ExpectRejected(() => Evaluate(baseline, duplicate));
        var missing = Clone();
        missing.Predictions.RemoveAt(0);
        ExpectRejected(() => Evaluate(baseline, missing));
    }

    public static void Save(EvaluationReport report, string directory, string predictionLabel)
    {
        Directory.CreateDirectory(directory);
        var json = JsonSerializer.Serialize(report, Json) + "\n";
        File.WriteAllText(Path.Combine(directory, "kh007-reference-evaluation.json"), json);
        var md = $"""
            # KH-007 — Evaluation deterministe du corpus de reference

            - Corpus: `{report.CorpusId}` ; {report.DocumentCount} documents ; {report.CaseCount} questions
            - Predictions: `{predictionLabel}` (aucune valeur issue d'Ollama n'est inferee)
            - `k = {report.K}` ; {report.AnswerableCases} questions documentaires ; {report.AbstentionCases} abstentions

            | Mesure | Resultat |
            | --- | ---: |
            | Precision@k (denominateur fixe k, questions repondables) | {report.PrecisionAtK:F6} |
            | Recall@k (questions repondables) | {report.RecallAtK:F6} |
            | MRR@k | {report.MrrAtK:F6} |
            | nDCG@k (pertinence binaire) | {report.NdcgAtK:F6} |
            | Exactitude des abstentions | {report.AbstentionAccuracy:F6} |
            | Accord du verdict answer/abstain | {report.VerdictAccuracy:F6} |
            | Precision des citations exactes | {report.EvidencePrecision:F6} |
            | Rappel des citations attendues | {report.EvidenceRecall:F6} |
            | Verdicts documentes stricts | {report.GroundedVerdictRate:F6} |

            Ces valeurs sont deterministes pour des predictions identiques. Le fichier oracle
            est un temoignage de bon fonctionnement de l'evaluateur, PAS une mesure du RAG.
            Les citations exigees sont des extraits textuels exacts. Aucune evaluation
            de la verite semantique d'une reponse libre n'est effectuee ici.
            Aucune question, reponse, document ni citation n'est copie dans les rapports.
            """;
        File.WriteAllText(Path.Combine(directory, "kh007-reference-evaluation.md"), md + "\n");
    }

    private static void ValidateGold(Corpus corpus, GoldSet set, string corpusId)
    {
        Require(set.SchemaVersion == 1 && set.CorpusId == corpusId &&
            corpus.Documents is { Count: > 0 } && set.Cases is { Count: > 0 },
            "Invalid reference schema.");
        var docIds = new HashSet<Guid>();
        foreach (var doc in corpus.Documents)
        {
            Require(doc.Id != Guid.Empty && docIds.Add(doc.Id) &&
                !string.IsNullOrWhiteSpace(doc.Title) && !string.IsNullOrWhiteSpace(doc.Content),
                "Invalid or duplicated reference document.");
        }
        var docs = corpus.Documents.ToDictionary(x => x.Id);
        var caseIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gold in set.Cases)
        {
            Require(ValidId(gold.Id) && caseIds.Add(gold.Id) &&
                !string.IsNullOrWhiteSpace(gold.Question) &&
                (gold.ExpectedVerdict is "answer" or "abstain") &&
                gold.RelevantDocuments is not null && gold.GoldEvidence is not null,
                "Invalid or duplicated reference case.");
            Require(gold.RelevantDocuments.Distinct().Count() == gold.RelevantDocuments.Count &&
                gold.RelevantDocuments.All(docs.ContainsKey), "Invalid reference relevance set.");
            if (gold.ExpectedVerdict == "abstain")
            {
                Require(gold.RelevantDocuments.Count == 0 && gold.GoldEvidence.Count == 0,
                    "Abstention must not contain gold evidence.");
            }
            else
            {
                Require(gold.RelevantDocuments.Count > 0 && gold.GoldEvidence.Count > 0 &&
                    gold.RelevantDocuments.All(id => gold.GoldEvidence.Any(e => e.DocumentId == id)),
                    "Answerable case needs evidence for each relevant document.");
            }
            var quotes = new HashSet<QuoteKey>();
            foreach (var evidence in gold.GoldEvidence)
            {
                Require(evidence.Quote is { Length: >= 15 } &&
                    gold.RelevantDocuments.Contains(evidence.DocumentId) &&
                    quotes.Add(new QuoteKey(evidence.DocumentId, evidence.Quote)) &&
                    docs[evidence.DocumentId].Content.Contains(evidence.Quote, StringComparison.Ordinal),
                    "Gold evidence must quote its source verbatim.");
            }
        }
    }

    private static void ValidatePredictions(
        PredictionSet predictions, GoldSet gold, Corpus corpus, string corpusId)
    {
        Require(predictions.SchemaVersion == 1 && predictions.CorpusId == corpusId &&
            predictions.Predictions is not null && predictions.Predictions.Count == gold.Cases.Count,
            "Prediction schema, identity or count mismatch.");
        var ids = gold.Cases.Select(c => c.Id).ToHashSet(StringComparer.Ordinal);
        var documentIds = corpus.Documents.Select(d => d.Id).ToHashSet();
        foreach (var prediction in predictions.Predictions)
        {
            Require(ValidId(prediction.Id) && ids.Remove(prediction.Id) &&
                (prediction.Verdict is "answer" or "abstain") &&
                prediction.RankedDocuments is not null && prediction.Evidence is not null,
                "Duplicate or invalid prediction.");
            Require(prediction.RankedDocuments.Count <= 100 &&
                prediction.RankedDocuments.Distinct().Count() == prediction.RankedDocuments.Count &&
                prediction.RankedDocuments.All(documentIds.Contains),
                "Ranked list contains duplicate or unknown documents.");
            Require(prediction.Evidence.Count <= 100 && prediction.Evidence.All(e =>
                e is not null && documentIds.Contains(e.DocumentId) && e.Quote is { Length: >= 1 and <= 10000 }) &&
                prediction.Evidence.Select(e => new QuoteKey(e.DocumentId, e.Quote)).Distinct().Count()
                    == prediction.Evidence.Count, "Invalid, duplicate or unknown evidence.");
            Require(prediction.Verdict != "abstain" || prediction.Evidence.Count == 0,
                "Abstentions must contain no evidence.");
        }
        Require(ids.Count == 0, "Missing prediction cases.");
    }

    private static bool ValidId(string? value) => value is { Length: >= 3 and <= 32 } &&
        value.All(c => c is >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');

    private static T Read<T>(string path) where T : class =>
        JsonSerializer.Deserialize<T>(File.ReadAllText(path), Json) ??
        throw new InvalidOperationException("JSON document cannot be empty.");

    private static void ExpectRejected(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Invalid sample was not rejected.");
    }

    private static void Require([DoesNotReturnIf(false)] bool condition, string safeError)
    {
        if (!condition) throw new InvalidOperationException(safeError);
    }

    private sealed record QuoteKey(Guid DocumentId, string Quote);
}

public sealed record Baseline(ReferenceManifest Manifest, Corpus Corpus,
    GoldSet Cases, PredictionSet Oracle);

public sealed class ReferenceManifest
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    [JsonPropertyName("corpus_id")] public string CorpusId { get; set; } = "";
    [JsonPropertyName("files")] public Dictionary<string, string> Files { get; set; } = [];
}
public sealed class Corpus
{
    public List<ReferenceDocument> Documents { get; set; } = [];
}
public sealed class ReferenceDocument
{
    public Guid Id { get; set; }
    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
}
public sealed class GoldSet
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    [JsonPropertyName("corpus_id")] public string CorpusId { get; set; } = "";
    [JsonPropertyName("cases")] public List<GoldCase> Cases { get; set; } = [];
}
public sealed class GoldCase
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("question")] public string Question { get; set; } = "";
    [JsonPropertyName("expected_verdict")] public string ExpectedVerdict { get; set; } = "";
    [JsonPropertyName("relevant_document_ids")] public List<Guid> RelevantDocuments { get; set; } = [];
    [JsonPropertyName("gold_evidence")] public List<ReferenceEvidence> GoldEvidence { get; set; } = [];
}
public sealed class PredictionSet
{
    [JsonPropertyName("schema_version")] public int SchemaVersion { get; set; }
    [JsonPropertyName("corpus_id")] public string CorpusId { get; set; } = "";
    [JsonPropertyName("predictions")] public List<Prediction> Predictions { get; set; } = [];
}
public sealed class Prediction
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("verdict")] public string Verdict { get; set; } = "";
    [JsonPropertyName("ranked_document_ids")] public List<Guid> RankedDocuments { get; set; } = [];
    [JsonPropertyName("evidence")] public List<ReferenceEvidence> Evidence { get; set; } = [];
}
public sealed class ReferenceEvidence
{
    [JsonPropertyName("document_id")] public Guid DocumentId { get; set; }
    [JsonPropertyName("quote")] public string Quote { get; set; } = "";
}
public sealed record CaseResult(string Id, string ExpectedVerdict, string PredictedVerdict,
    bool VerdictCorrect, double PrecisionAtK, double RecallAtK,
    double MrrAtK, double NdcgAtK, int MatchedQuotes, int RequiredQuotes,
    int PredictedQuotes, double QuotePrecision, double QuoteRecall, bool SupportedVerdict);
public sealed record EvaluationReport(int SchemaVersion, string CorpusId, int K,
    int DocumentCount, int CaseCount, int AnswerableCases, int AbstentionCases,
    double PrecisionAtK, double RecallAtK, double MrrAtK, double NdcgAtK,
    double AbstentionAccuracy, double VerdictAccuracy, double EvidencePrecision,
    double EvidenceRecall, double GroundedVerdictRate, IReadOnlyList<CaseResult> Cases);
