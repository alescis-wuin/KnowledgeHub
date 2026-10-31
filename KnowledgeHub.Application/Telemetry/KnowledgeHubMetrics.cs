using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace KnowledgeHub.Application.Telemetry;

/// <summary>
/// Application metrics with a fixed vocabulary of low-cardinality attributes.
/// No user inputs, document identifiers, model output or exception text may be tags.
/// The meter is independent of the configured tracing exporter.
/// </summary>
public static class KnowledgeHubMetrics
{
    public const string MeterName = "KnowledgeHub.Metrics";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    private static readonly Counter<long> RagRequests =
        Meter.CreateCounter<long>("kh.rag.requests", unit: "{request}");
    private static readonly Histogram<double> RagDuration =
        Meter.CreateHistogram<double>("kh.rag.duration", unit: "ms");
    private static readonly Histogram<double> RagStageDuration =
        Meter.CreateHistogram<double>("kh.rag.stage.duration", unit: "ms");

    private static readonly Counter<long> RetrievalRequests =
        Meter.CreateCounter<long>("kh.retrieval.requests", unit: "{request}");
    private static readonly Histogram<double> RetrievalDuration =
        Meter.CreateHistogram<double>("kh.retrieval.duration", unit: "ms");
    private static readonly Histogram<long> RetrievalResults =
        Meter.CreateHistogram<long>("kh.retrieval.results", unit: "{result}");

    private static readonly Counter<long> CacheLookups =
        Meter.CreateCounter<long>("kh.vector_cache.lookups", unit: "{lookup}");
    private static readonly Histogram<double> CacheLoadDuration =
        Meter.CreateHistogram<double>("kh.vector_cache.load.duration", unit: "ms");
    private static readonly Histogram<long> CacheEntries =
        Meter.CreateHistogram<long>("kh.vector_cache.entries", unit: "{entry}");
    private static readonly Counter<long> CacheInvalidations =
        Meter.CreateCounter<long>("kh.vector_cache.invalidations", unit: "{operation}");

    private static readonly Counter<long> OllamaRequests =
        Meter.CreateCounter<long>("kh.ollama.requests", unit: "{request}");
    private static readonly Histogram<double> OllamaDuration =
        Meter.CreateHistogram<double>("kh.ollama.duration", unit: "ms");

    public static void RecordRagRequest(string outcome, double elapsedMilliseconds)
    {
        outcome = RequireRagOutcome(outcome);
        RagRequests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
        RagDuration.Record(elapsedMilliseconds,
            new KeyValuePair<string, object?>("outcome", outcome));
    }

    public static Task<T> MeasureStageAsync<T>(
        string stage,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        stage = RequireStage(stage);
        return ObserveAsync(action, cancellationToken, (outcome, elapsed) =>
            RagStageDuration.Record(elapsed,
                new KeyValuePair<string, object?>("stage", stage),
                new KeyValuePair<string, object?>("outcome", outcome)));
    }

    public static T MeasureStage<T>(string stage, Func<T> action)
    {
        stage = RequireStage(stage);
        ArgumentNullException.ThrowIfNull(action);
        var started = Stopwatch.GetTimestamp();
        var outcome = "error";
        try
        {
            var result = action();
            outcome = "ok";
            return result;
        }
        finally
        {
            RagStageDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("stage", stage),
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    public static async Task<IReadOnlyCollection<T>> MeasureRetrievalAsync<T>(
        Func<Task<IReadOnlyCollection<T>>> action,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(action);
        var started = Stopwatch.GetTimestamp();
        var outcome = "error";
        try
        {
            var result = await action();
            RetrievalResults.Record(result.Count);
            outcome = "ok";
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            RetrievalRequests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));
            RetrievalDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds,
                new KeyValuePair<string, object?>("outcome", outcome));
        }
    }

    public static void RecordCacheLookup(bool hit) =>
        CacheLookups.Add(1,
            new KeyValuePair<string, object?>("result", hit ? "hit" : "miss"));

    public static void RecordCacheLoad(
        string outcome, double elapsedMilliseconds, int entryCount)
    {
        outcome = RequireOperationOutcome(outcome);
        CacheLoadDuration.Record(elapsedMilliseconds,
            new KeyValuePair<string, object?>("outcome", outcome));
        if (outcome == "ok")
        {
            CacheEntries.Record(entryCount);
        }
    }

    public static void RecordCacheInvalidation() => CacheInvalidations.Add(1);

    public static Task<T> MeasureOllamaAsync<T>(
        string operation,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        operation = operation switch
        {
            "chat" => "chat",
            "embedding" => "embedding",
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
        return ObserveAsync(action, cancellationToken, (outcome, elapsed) =>
        {
            OllamaRequests.Add(1,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("outcome", outcome));
            OllamaDuration.Record(elapsed,
                new KeyValuePair<string, object?>("operation", operation),
                new KeyValuePair<string, object?>("outcome", outcome));
        });
    }

    private static async Task<T> ObserveAsync<T>(
        Func<Task<T>> action,
        CancellationToken cancellationToken,
        Action<string, double> record)
    {
        ArgumentNullException.ThrowIfNull(action);
        var started = Stopwatch.GetTimestamp();
        var outcome = "error";
        try
        {
            var result = await action();
            outcome = "ok";
            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            outcome = "cancelled";
            throw;
        }
        finally
        {
            record(outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    private static string RequireStage(string stage) => stage switch
    {
        "retrieve" => "retrieve",
        "context" => "context",
        "generate" => "generate",
        _ => throw new ArgumentOutOfRangeException(nameof(stage))
    };

    private static string RequireRagOutcome(string outcome) => outcome switch
    {
        "ok" => "ok",
        "no_context" => "no_context",
        "cancelled" => "cancelled",
        "error" => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };

    private static string RequireOperationOutcome(string outcome) => outcome switch
    {
        "ok" => "ok",
        "cancelled" => "cancelled",
        "error" => "error",
        _ => throw new ArgumentOutOfRangeException(nameof(outcome))
    };
}
