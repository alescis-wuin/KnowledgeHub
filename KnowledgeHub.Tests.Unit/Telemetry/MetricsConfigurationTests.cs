using KnowledgeHub.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class MetricsConfigurationTests
{
    [Theory]
    [InlineData("http://example.org:4317")]
    [InlineData("https://username:password@example.org:4317")]
    [InlineData("https://example.org:4317/?token=PRIVATE_METRIC_TOKEN")]
    [InlineData("https://example.org:4317/#fragment")]
    [InlineData("file:///tmp/collector")]
    public void Endpoint_RejectsUnsafeDestinationsWithoutLeakingValues(string endpoint)
    {
        var configuration = Configuration(new()
        {
            ["Metrics:OtlpEndpoint"] = endpoint
        });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKnowledgeHubMetrics(configuration));
        Assert.Contains("Metrics:OtlpEndpoint", exception.Message);
        Assert.DoesNotContain(endpoint, exception.Message);
        Assert.DoesNotContain("PRIVATE_METRIC_TOKEN", exception.Message);
    }

    [Theory]
    [InlineData("http://127.0.0.1:4317")]
    [InlineData("http://localhost:4317")]
    [InlineData("https://metrics.example.org:4317")]
    public void Endpoint_AllowsLoopbackHttpOrHttps(string endpoint)
    {
        var services = new ServiceCollection();
        Assert.Same(services, services.AddKnowledgeHubMetrics(Configuration(new()
        {
            ["Metrics:OtlpEndpoint"] = endpoint
        })));
    }

    [Fact]
    public void ExportIsOptIn_AndDisabledModeDoesNotRegisterProvider()
    {
        var empty = new ServiceCollection();
        Assert.Same(empty, empty.AddKnowledgeHubMetrics(Configuration(new())));
        Assert.NotEmpty(empty);

        var disabled = new ServiceCollection();
        Assert.Same(disabled, disabled.AddKnowledgeHubMetrics(Configuration(new()
        {
            ["Metrics:Enabled"] = "false",
            ["Metrics:OtlpEndpoint"] = "https://example.org"
        })));
        Assert.Empty(disabled);
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
