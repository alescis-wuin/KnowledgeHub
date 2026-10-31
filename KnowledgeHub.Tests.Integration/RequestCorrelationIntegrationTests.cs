using KnowledgeHub.Api.Middleware;
using Microsoft.AspNetCore.Mvc.Testing;

namespace KnowledgeHub.Tests.Integration;

public sealed class RequestCorrelationIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public RequestCorrelationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ResponseEchoesValidatedCorrelationId()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/documents");
        request.Headers.TryAddWithoutValidation(
            RequestCorrelationLoggingMiddleware.CorrelationHeader,
            "kh005-validated-123456");
        using var response = await client.SendAsync(request);

        Assert.True(response.IsSuccessStatusCode);
        Assert.Equal("kh005-validated-123456",
            Assert.Single(response.Headers.GetValues(
                RequestCorrelationLoggingMiddleware.CorrelationHeader)));
    }
}
