using System.Net;
using System.Text;
using System.Text.Json;
using KnowledgeHub.Application.Verification;
using KnowledgeHub.Infrastructure.AI;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Services;

public sealed class OllamaClaimProposalGeneratorTests
{
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");

    private static EvidenceRetrievalSnapshot Snapshot(string content = "Document autorisé") =>
        new([new EvidenceSnapshot(DocumentId, ChunkId, content)],
            new Dictionary<Guid, string> { [DocumentId] = new string('a', 64) });

    [Fact]
    public async Task RequestUsesOnlyAuthorizedServerSnapshotAndStrictJsonInstruction()
    {
        var handler = new StubHandler("{\"message\":{\"content\":\"{\\\"schemaVersion\\\":1}\"}}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var result = await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question test", Snapshot());

        Assert.Equal("{\"schemaVersion\":1}", result);
        Assert.Equal(1, handler.Calls);
        using var request = JsonDocument.Parse(handler.RequestBody!);
        var messages = request.RootElement.GetProperty("messages");
        Assert.Contains("no markdown", messages[0].GetProperty("content").GetString()!,
            StringComparison.OrdinalIgnoreCase);
        var userContent = messages[1].GetProperty("content").GetString()!;
        var records = userContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, records.Length);
        Assert.Equal("Question: question test", records[0]);
        Assert.Equal("Authorized persisted chunks (JSON Lines):", records[1]);
        using var authorized = JsonDocument.Parse(records[2]);
        Assert.Equal(DocumentId, authorized.RootElement.GetProperty("documentId").GetGuid());
        Assert.Equal(ChunkId, authorized.RootElement.GetProperty("chunkId").GetGuid());
        Assert.Equal("Document autorisé", authorized.RootElement.GetProperty("content").GetString());
        Assert.Equal(Snapshot().AllowedChunks[ChunkId].ChunkSha256,
            authorized.RootElement.GetProperty("chunkSha256").GetString());
    }

    [Fact]
    public async Task OversizedChunkDoesNotContactOllamaOrTruncateOffsets()
    {
        var handler = new StubHandler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        var result = await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot(new string('x', 25000)));
        Assert.Null(result);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public async Task ReasoningOnlyResponseCannotBecomeUntrustedProposal()
    {
        var handler = new StubHandler(
            "{\"message\":{\"thinking\":\"SECRET\",\"content\":\"\"}}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        Assert.Null(await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot()));
    }

    [Fact]
    public async Task ServerFailureDoesNotReturnContent()
    {
        var handler = new StubHandler("PRIVATE", HttpStatusCode.ServiceUnavailable);
        using var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:11434") };
        Assert.Null(await new OllamaClaimProposalGenerator(http).GenerateAsync(
            "question", Snapshot()));
    }

    private sealed class StubHandler(string body, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            RequestBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };
        }
    }
}
