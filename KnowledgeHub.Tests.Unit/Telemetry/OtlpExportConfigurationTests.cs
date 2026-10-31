using KnowledgeHub.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class OtlpExportConfigurationTests
{
    [Theory]
    [InlineData("Tracing:OtlpProtocol", "http/protobuf", "http://127.0.0.1:4318/v1/traces", true)]
    [InlineData("Metrics:OtlpProtocol", "http/protobuf", "http://127.0.0.1:4318/v1/metrics", false)]
    public void HttpProtobufLocalSignalPath_IsAccepted(
        string protocolKey, string protocol, string endpoint, bool tracing)
    {
        var endpointKey = tracing ? "Tracing:OtlpEndpoint" : "Metrics:OtlpEndpoint";
        var services = new ServiceCollection();
        Configure(services, Config(new()
        {
            [protocolKey] = protocol,
            [endpointKey] = endpoint
        }), tracing);
        Assert.NotEmpty(services);
    }

    [Theory]
    [InlineData("Tracing", "http://127.0.0.1:4318/v1/metrics")]
    [InlineData("Metrics", "http://127.0.0.1:4318/v1/traces")]
    [InlineData("Tracing", "http://127.0.0.1:4318/")]
    [InlineData("Metrics", "http://127.0.0.1:4318/")]
    public void HttpProtobufWrongSignalPath_IsRejected(string section, string endpoint)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Configure(new ServiceCollection(), Config(new()
            {
                [section + ":OtlpProtocol"] = "http/protobuf",
                [section + ":OtlpEndpoint"] = endpoint
            }), section == "Tracing"));
        Assert.Contains(section + ":OtlpEndpoint", exception.Message);
        Assert.DoesNotContain(endpoint, exception.Message);
    }

    [Theory]
    [InlineData("Tracing")]
    [InlineData("Metrics")]
    public void UnknownTransport_IsRejectedWithSanitizedMessage(string section)
    {
        const string privateValue = "PRIVATE_UNSUPPORTED_PROTOCOL";
        var exception = Assert.Throws<InvalidOperationException>(() =>
            Configure(new ServiceCollection(), Config(new()
            {
                [section + ":OtlpProtocol"] = privateValue
            }), section == "Tracing"));
        Assert.Contains(section + ":OtlpProtocol", exception.Message);
        Assert.DoesNotContain(privateValue, exception.Message);
    }

    [Theory]
    [InlineData("999")]
    [InlineData("60001")]
    [InlineData("-1")]
    public void MetricsExportInterval_OutOfBounds_IsRejected(string interval)
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKnowledgeHubMetrics(Config(new()
            {
                ["Metrics:ExportIntervalMilliseconds"] = interval
            })));
        Assert.Contains("Metrics:ExportIntervalMilliseconds", exception.Message);
    }

    private static void Configure(IServiceCollection services,
        IConfiguration configuration, bool tracing)
    {
        if (tracing)
        {
            services.AddKnowledgeHubTracing(configuration);
        }
        else
        {
            services.AddKnowledgeHubMetrics(configuration);
        }
    }

    private static IConfiguration Config(Dictionary<string, string?> settings) =>
        new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
}
