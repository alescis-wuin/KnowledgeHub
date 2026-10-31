using System.Diagnostics;
using System.Text.Json;
using KnowledgeHub.Application.Telemetry;

namespace KnowledgeHub.Infrastructure.AI;

/// <summary>
/// Reads only allowlisted numeric Ollama response fields. Unrecognized fields
/// (including model names, thinking, prompts and message content) are ignored.
/// Malformed optional usage metadata never invalidates a usable API response.
/// </summary>
internal static class OllamaNativeTelemetry
{
    public static void Record(
        string operation,
        IReadOnlyDictionary<string, JsonElement>? responseFields)
    {
        if (responseFields is null || responseFields.Count == 0)
        {
            return;
        }

        var usage = new OllamaNativeUsage(
            ReadNonNegative(responseFields, "total_duration"),
            ReadNonNegative(responseFields, "load_duration"),
            ReadNonNegative(responseFields, "prompt_eval_duration"),
            ReadNonNegative(responseFields, "eval_duration"),
            ReadNonNegative(responseFields, "prompt_eval_count"),
            ReadNonNegative(responseFields, "prompt_eval_cached_count"),
            ReadNonNegative(responseFields, "eval_count"));

        KnowledgeHubMetrics.RecordOllamaNativeUsage(operation, usage);

        // Only numerical values, never a raw response, exception, model ID or
        // user-controlled string, can become a trace attribute.
        var activity = Activity.Current;
        if (activity is null)
        {
            return;
        }

        SetCount(activity, "kh.ollama.prompt_tokens", usage.PromptTokenCount);
        SetCount(activity, "kh.ollama.cached_prompt_tokens", usage.CachedPromptTokenCount);
        SetCount(activity, "kh.ollama.output_tokens", usage.OutputTokenCount);
        SetDuration(activity, "kh.ollama.total_duration_ms", usage.TotalDurationNanoseconds);
        SetDuration(activity, "kh.ollama.load_duration_ms", usage.LoadDurationNanoseconds);
        SetDuration(activity, "kh.ollama.prompt_eval_duration_ms",
            usage.PromptEvaluationDurationNanoseconds);
        SetDuration(activity, "kh.ollama.eval_duration_ms", usage.GenerationDurationNanoseconds);

        if (operation == "chat" &&
            usage.OutputTokenCount is > 0 &&
            usage.GenerationDurationNanoseconds is > 0)
        {
            activity.SetTag("kh.ollama.output_tokens_per_second",
                (double)usage.OutputTokenCount.Value * 1_000_000_000d /
                usage.GenerationDurationNanoseconds.Value);
        }
    }

    private static long? ReadNonNegative(
        IReadOnlyDictionary<string, JsonElement> fields,
        string key)
    {
        if (!fields.TryGetValue(key, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out var number) || number < 0)
        {
            return null;
        }

        return number;
    }

    private static void SetCount(Activity activity, string key, long? count)
    {
        if (count.HasValue)
        {
            activity.SetTag(key, count.Value);
        }
    }

    private static void SetDuration(Activity activity, string key, long? nanoseconds)
    {
        if (nanoseconds.HasValue)
        {
            activity.SetTag(key, nanoseconds.Value / 1_000_000d);
        }
    }
}
