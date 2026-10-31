using KnowledgeHub.Api.Telemetry;
using KnowledgeHub.Application.Telemetry;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace KnowledgeHub.Api.Extensions;

public static class TracingServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeHubTracing(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (configuration.GetValue<bool?>("Tracing:Enabled") == false)
        {
            return services;
        }

        var ratio = configuration.GetValue<double?>("Tracing:SampleRatio") ?? 1.0;
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1)
        {
            throw new InvalidOperationException(
                "Tracing:SampleRatio must be a finite number from 0 to 1.");
        }

        Uri? endpoint = null;
        var configuredEndpoint = configuration["Tracing:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps &&
                 !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)) ||
                endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 ||
                endpoint.Fragment.Length != 0)
            {
                throw new InvalidOperationException(
                    "Tracing:OtlpEndpoint must be HTTPS or loopback HTTP without credentials or query.");
            }
        }

        // Propagate W3C tracecontext only; never forward caller-supplied baggage.
        Sdk.SetDefaultTextMapPropagator(new TraceContextPropagator());

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("KnowledgeHub.Api"))
            .WithTracing(tracing =>
            {
                tracing.SetSampler(new ParentBasedSampler(
                    new TraceIdRatioBasedSampler(ratio)));
                tracing.AddSource(KnowledgeHubActivities.SourceName);
                tracing.AddAspNetCoreInstrumentation(options =>
                {
                    options.RecordException = false;
                });
                tracing.AddHttpClientInstrumentation(options =>
                {
                    options.RecordException = false;
                });
                tracing.AddProcessor(new SafeTraceProcessor());

                // Export is disabled by default. The operator must opt in.
                if (endpoint is not null)
                {
                    tracing.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
            });
        return services;
    }
}
