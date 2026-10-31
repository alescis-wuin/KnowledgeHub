using System.Diagnostics;
using KnowledgeHub.Api.Telemetry;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class SafeTraceProcessorTests
{
    [Theory]
    [InlineData(ActivityKind.Client)]
    [InlineData(ActivityKind.Server)]
    public void OnEnd_RemovesSensitiveUrlHeadersAndDisplayName(ActivityKind kind)
    {
        var activity = new Activity("POST /api/rag/ask?question=PRIVATE_QUESTION");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        activity.SetTag("url.full", "http://localhost/?question=PRIVATE_QUESTION");
        activity.SetTag("url.path", "/doc/PRIVATE_DOCUMENT_IDENTIFIER");
        activity.SetTag("http.request.header.authorization", "PRIVATE_TOKEN");
        activity.SetTag("http.response.status_code", 200);
        activity.SetTag("http.route", "api/rag/ask");
        activity.SetTag("kh.rag.source_count", 2);
        activity.SetTag("unexpected.data", "PRIVATE_CONTEXT");
        activity.Start();
        activity.Stop();

        // A manually-created Activity's kind is Internal. Use ActivitySource
        // for a real Client/Server activity and preserve its requested kind.
        var actual = CreateKindActivity(kind);
        actual.SetTag("url.full", "http://localhost/?question=PRIVATE_QUESTION");
        actual.SetTag("http.request.header.authorization", "PRIVATE_TOKEN");
        actual.SetTag("http.response.status_code", 200);
        actual.SetTag("http.route", "api/rag/ask");
        actual.Stop();
        new SafeTraceProcessor().OnEnd(actual);

        var snapshot = actual.DisplayName + string.Join(" ",
            actual.TagObjects.Select(x => $"{x.Key}:{x.Value}"));
        Assert.DoesNotContain("PRIVATE_QUESTION", snapshot);
        Assert.DoesNotContain("PRIVATE_TOKEN", snapshot);
        Assert.Contains("http.route", snapshot);
        Assert.Contains("200", snapshot);
        Assert.Equal(kind == ActivityKind.Client ? "http.client.request" : "http.server.request",
            actual.DisplayName);
        activity.Dispose();
        actual.Dispose();
    }

    [Fact]
    public void OnEnd_UnknownInternalSpanCannotExportUntrustedNameOrTag()
    {
        using var activity = new Activity("PRIVATE_QUESTION_IN_SPAN_NAME");
        activity.SetTag("unexpected.data", "PRIVATE_DOCUMENT");
        activity.SetTag("kh.operation.outcome", "ok");
        activity.Start();
        activity.Stop();
        new SafeTraceProcessor().OnEnd(activity);
        Assert.Equal("internal.operation", activity.DisplayName);
        Assert.Null(activity.GetTagItem("unexpected.data"));
        Assert.Equal("ok", activity.GetTagItem("kh.operation.outcome"));
    }

    private static Activity CreateKindActivity(ActivityKind kind)
    {
        // Activity's internal ctor cannot set Kind; ActivitySource can.
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded
        };
        ActivitySource.AddActivityListener(listener);
        using var source = new ActivitySource("KnowledgeHub.Tests.TraceProcessor");
        return source.StartActivity("PRIVATE_QUESTION_IN_DISPLAY_NAME", kind)
            ?? throw new InvalidOperationException("Test ActivitySource was not sampled.");
    }
}
