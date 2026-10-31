using KnowledgeHub.Application.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;

namespace KnowledgeHub.Api.Extensions;

/// <summary>
/// Opt-in OTLP metrics export; the application meter remains usable without a collector.
/// Deliberately excludes automatic HTTP labels and all variable user-supplied tags.
/// </summary>
public static class MetricsServiceCollectionExtensions
{
    public static IServiceCollection AddKnowledgeHubMetrics(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.GetValue<bool?>("Metrics:Enabled") == false)
        {
            return services;
        }

        Uri? endpoint = null;
        var configuredEndpoint = configuration["Metrics:OtlpEndpoint"];
        if (!string.IsNullOrWhiteSpace(configuredEndpoint))
        {
            if (!Uri.TryCreate(configuredEndpoint, UriKind.Absolute, out endpoint) ||
                (endpoint.Scheme != Uri.UriSchemeHttps &&
                 !(endpoint.Scheme == Uri.UriSchemeHttp && endpoint.IsLoopback)) ||
                endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 ||
                endpoint.Fragment.Length != 0)
            {
                throw new InvalidOperationException(
                    "Metrics:OtlpEndpoint must be HTTPS or loopback HTTP without credentials or query.");
            }
        }

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("KnowledgeHub.Api"))
            .WithMetrics(metrics =>
            {
                metrics.AddMeter(KnowledgeHubMetrics.MeterName);
                // Unlike tracing, metrics are not sampled; every invocation contributes.
                // Export is off unless the operator supplies an explicit safe endpoint.
                if (endpoint is not null)
                {
                    metrics.AddOtlpExporter(options => options.Endpoint = endpoint);
                }
            });

        return services;
    }
}
