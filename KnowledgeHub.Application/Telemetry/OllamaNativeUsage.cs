namespace KnowledgeHub.Application.Telemetry;

/// <summary>
/// Numeric timing and token metadata returned by Ollama, not timings observed by
/// the HTTP client. Missing, negative and non-integral fields are represented
/// as null, never as inferred zeroes. Timings are raw nanoseconds.
/// </summary>
public readonly record struct OllamaNativeUsage(
    long? TotalDurationNanoseconds,
    long? LoadDurationNanoseconds,
    long? PromptEvaluationDurationNanoseconds,
    long? GenerationDurationNanoseconds,
    long? PromptTokenCount,
    long? CachedPromptTokenCount,
    long? OutputTokenCount);
