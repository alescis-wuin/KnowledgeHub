using System.Globalization;
using System.Net;
using System.Text;
using KnowledgeHub.Infrastructure.AI;

namespace KnowledgeHub.Tests.Unit.Services;

// Only these tests redirect Console.Out, so they must not overlap another test.
[CollectionDefinition("OllamaConsoleIsolation", DisableParallelization = true)]
public sealed class OllamaConsoleIsolationCollection
{
}

[Collection("OllamaConsoleIsolation")]
public sealed class OllamaChatCompletionServiceTests
{
    [Fact]
    public async Task ValidAnswer_ReturnsContentWithoutWritingRawResponseOrThinking()
    {
        const string content = "Réponse utilisable";
        const string privateThinking = "PRIVATE_THINKING_SENTINEL_DO_NOT_LOG";
        using var http = CreateClient(
            "{\"message\":{\"content\":\"Réponse utilisable\",\"thinking\":\"" +
            privateThinking + "\"}}");
        var service = new OllamaChatCompletionService(http);
        using var writer = new StringWriter(CultureInfo.InvariantCulture);
        var previous = Console.Out;
        string answer;
        try
        {
            Console.SetOut(writer);
            answer = await service.GenerateAnswerAsync(
                "PRIVATE_QUESTION", "PRIVATE_DOCUMENT_CONTEXT");
        }
        finally
        {
            Console.SetOut(previous);
        }

        Assert.Equal(content, answer);
        Assert.Equal(string.Empty, writer.ToString());
        Assert.DoesNotContain(privateThinking, answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("{")]
    [InlineData("[]")]
    [InlineData("{\"message\":{\"content\":123}}")]
    public async Task InvalidJson_ThrowsSanitizedException(string payload)
    {
        using var http = CreateClient(payload);
        var service = new OllamaChatCompletionService(http);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GenerateAnswerAsync("QUESTION_PRIVATE", "CONTEXT_PRIVATE"));

        Assert.Equal("Ollama chat returned invalid JSON.", exception.Message);
        Assert.DoesNotContain(payload, exception.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"message\":null}")]
    [InlineData("{\"message\":{\"content\":\"\"}}")]
    [InlineData("{\"message\":{\"content\":\"  \",\"thinking\":\"PRIVATE_THINKING_SENTINEL_DO_NOT_LOG\"}}")]
    public async Task MissingContent_DoesNotReturnThinkingOrEmptyAnswer(string payload)
    {
        using var http = CreateClient(payload);
        var service = new OllamaChatCompletionService(http);

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.GenerateAnswerAsync("QUESTION_PRIVATE", "CONTEXT_PRIVATE"));

        Assert.Equal("Ollama chat did not return a usable answer.", exception.Message);
        Assert.DoesNotContain("PRIVATE_THINKING_SENTINEL", exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task HttpError_RemainsAnHttpErrorWithoutReadingItsBody()
    {
        using var http = CreateClient(
            "PRIVATE_RESPONSE_BODY_SENTINEL", HttpStatusCode.ServiceUnavailable);
        var service = new OllamaChatCompletionService(http);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GenerateAnswerAsync("QUESTION_PRIVATE", "CONTEXT_PRIVATE"));

        Assert.Equal(HttpStatusCode.ServiceUnavailable, exception.StatusCode);
        Assert.DoesNotContain("PRIVATE_RESPONSE_BODY_SENTINEL", exception.ToString(),
            StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(
        string body,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        return new HttpClient(new FakeOllamaHandler(body, status))
        {
            BaseAddress = new Uri("http://localhost:11434")
        };
    }

    private sealed class FakeOllamaHandler(
        string body,
        HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/chat", request.RequestUri?.AbsolutePath);
            cancellationToken.ThrowIfCancellationRequested();

            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }
}
