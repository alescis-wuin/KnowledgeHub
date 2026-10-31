using System.Diagnostics;

namespace KnowledgeHub.Application.Telemetry;

/// <summary>
/// Shared dependency-free activity source and sanitized error categorization.
/// Never attach prompts, document contents, answers, URLs or exception messages.
/// </summary>
public static class KnowledgeHubActivities
{
    public const string SourceName = "KnowledgeHub";

    public static readonly ActivitySource Source = new(SourceName);

    public static async Task<T> TraceAsync<T>(
        string operation,
        Func<Task<T>> action,
        CancellationToken cancellationToken = default)
    {
        using var activity = Source.StartActivity(operation);
        try
        {
            var result = await action();
            activity?.SetTag("kh.operation.outcome", "ok");
            return result;
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, cancellationToken);
            throw;
        }
    }

    public static async Task TraceAsync(
        string operation,
        Func<Task> action,
        CancellationToken cancellationToken = default)
    {
        using var activity = Source.StartActivity(operation);
        try
        {
            await action();
            activity?.SetTag("kh.operation.outcome", "ok");
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, cancellationToken);
            throw;
        }
    }

    public static T Trace<T>(string operation, Func<T> action)
    {
        using var activity = Source.StartActivity(operation);
        try
        {
            var result = action();
            activity?.SetTag("kh.operation.outcome", "ok");
            return result;
        }
        catch (Exception exception)
        {
            RecordFailure(activity, exception, CancellationToken.None);
            throw;
        }
    }

    private static void RecordFailure(
        Activity? activity,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (activity is null)
        {
            return;
        }

        if (exception is OperationCanceledException && cancellationToken.IsCancellationRequested)
        {
            activity.SetTag("kh.operation.outcome", "cancelled");
            return;
        }

        // Error category only: exception.Message and stack may contain private text.
        activity.SetTag("kh.operation.outcome", "error");
        activity.SetTag("error.type", exception.GetType().Name);
        activity.SetStatus(ActivityStatusCode.Error);
    }
}
