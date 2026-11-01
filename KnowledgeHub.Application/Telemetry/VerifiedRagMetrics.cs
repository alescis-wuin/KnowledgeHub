using System.Diagnostics.Metrics;

namespace KnowledgeHub.Application.Telemetry;

/// <summary>
/// Fixed-cardinality outcome metric. Never uses questions, model strings,
/// document IDs, evidence contents, or exception messages as labels.
/// </summary>
public static class VerifiedRagMetrics
{
    private static readonly Counter<long> PublicResponses =
        KnowledgeHubMetrics.Meter.CreateCounter<long>(
            "kh.rag.verified.responses", unit: "{response}");

    public static void Record(string verdict)
    {
        var safeVerdict = verdict switch
        {
            "ANSWER" => "answer",
            "PARTIAL" => "partial",
            "CONFLICT" => "conflict",
            _ => "abstain"
        };
        PublicResponses.Add(1,
            new KeyValuePair<string, object?>("verdict", safeVerdict));
    }
}
