using System.Diagnostics;
using OpenTelemetry;

namespace KnowledgeHub.Api.Telemetry;

/// <summary>
/// Defensive allowlist applied before spans reach any configured OTLP exporter.
/// In particular, native HttpClient spans can contain url.full or query values.
/// </summary>
public sealed class SafeTraceProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "http.request.method",
        "http.response.status_code",
        "http.route",
        "error.type",
        "kh.operation.outcome",
        "kh.rag.source_count",
        "kh.retrieval.top_k",
        "kh.retrieval.result_count",
        "kh.cache.hit",
        "kh.cache.entry_count",
        "kh.embedding.chunk_count",
        "kh.embedding.dimension",
        "kh.document.chunk_count",
        "kh.document.reindexed",
        "kh.import.outcome"
    };

    private static readonly HashSet<string> AllowedOperations = new(StringComparer.Ordinal)
    {
        "rag.ask", "rag.retrieve", "rag.context", "rag.generate",
        "vector_search.search", "vector_search.rank", "vector_index.load",
        "vector_index.invalidate", "ollama.chat", "ollama.embed",
        "document.index", "document.import", "document.update",
        "document.index.repair", "document.embed_batch"
    };

    public override void OnEnd(Activity activity)
    {
        // Snapshot keys first; SetTag(key, null) removes them from the activity.
        foreach (var tag in activity.TagObjects.ToArray())
        {
            if (!AllowedTags.Contains(tag.Key))
            {
                activity.SetTag(tag.Key, (object?)null);
            }
        }

        // A client/server DisplayName may otherwise contain a raw URL.
        activity.DisplayName = activity.Kind switch
        {
            ActivityKind.Server => "http.server.request",
            ActivityKind.Client => "http.client.request",
            _ when AllowedOperations.Contains(activity.DisplayName) => activity.DisplayName,
            _ => "internal.operation"
        };
    }
}
