using KnowledgeHub.Api.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace KnowledgeHub.Tests.Unit.Telemetry;

public sealed class TracingConfigurationTests
{
    [Theory]
    [InlineData("-0.1")]
    [InlineData("1.1")]
    public void SampleRatio_OutsideRange_IsRejected(string ratio)
    {
        var configuration = BuildConfiguration(new()
        {
            ["Tracing:SampleRatio"] = ratio
        });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKnowledgeHubTracing(configuration));
        Assert.Contains("Tracing:SampleRatio", exception.Message);
    }

    [Theory]
    [InlineData("http://example.net:4317")]
    [InlineData("https://username:password@example.net:4317")]
    [InlineData("https://example.net:4317/?token=PRIVATE_TOKEN")]
    public void OtlpEndpoint_UnsafeDestination_IsRejectedWithoutLeakingValue(string url)
    {
        var configuration = BuildConfiguration(new()
        {
            ["Tracing:OtlpEndpoint"] = url
        });
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AddKnowledgeHubTracing(configuration));
        Assert.Contains("Tracing:OtlpEndpoint", exception.Message);
        Assert.DoesNotContain(url, exception.Message);
        Assert.DoesNotContain("PRIVATE_TOKEN", exception.Message);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> data) =>
        new ConfigurationBuilder().AddInMemoryCollection(data).Build();
}
