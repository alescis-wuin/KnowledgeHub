using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Verification;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace KnowledgeHub.Tests.Integration.Verification;

public sealed class VerifiedRagEndpointIntegrationTests
    : IClassFixture<CustomWebApplicationFactory>
{
    private const string PolicyId = "integration-qualified-policy";
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private const string Content = "Preuve publique sans secrets.";
    private readonly CustomWebApplicationFactory _factory;

    public VerifiedRagEndpointIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnconfiguredProductionEndpointAbstainsWithoutClaims()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.PostAsJsonAsync(
            "/api/rag/ask/verified", new { question = "PRIVATE QUESTION" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ABSTAIN", json.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(0, json.RootElement.GetProperty("claims").GetArrayLength());
        Assert.DoesNotContain("PRIVATE QUESTION", json.RootElement.ToString(),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false, "PARTIAL", 1)]
    [InlineData(true, "ABSTAIN", 0)]
    public async Task QualifiedHttpPolicyFiltersClaimsAndFinalRevalidation(
        bool becomesStale, string expectedVerdict, int expectedClaims)
    {
        using var webFactory = _factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Verification:TrustedSemanticPolicyId"] = PolicyId
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IVectorSearchService>();
                services.RemoveAll<IEvidenceSnapshotProvider>();
                services.RemoveAll<IClaimProposalGenerator>();
                services.RemoveAll<IQualifiedEvidenceAssessor>();
                services.AddScoped<IVectorSearchService, FakeSearch>();
                services.AddScoped<IEvidenceSnapshotProvider>(
                    _ => new FakeSnapshots(becomesStale));
                services.AddScoped<IClaimProposalGenerator, FakeGenerator>();
                services.AddScoped<IQualifiedEvidenceAssessor, FakeAssessor>();
            });
        });

        using var client = webFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.PostAsJsonAsync(
            "/api/rag/ask/verified", new { question = "PUBLIC TEST QUESTION" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var serialized = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(serialized);
        Assert.Equal(expectedVerdict, json.RootElement.GetProperty("verdict").GetString());
        Assert.Equal(expectedClaims, json.RootElement.GetProperty("claims").GetArrayLength());
        Assert.DoesNotContain("INVENTED SECRET FACT", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("PUBLIC TEST QUESTION", serialized, StringComparison.Ordinal);
    }

    private static EvidenceRetrievalSnapshot Snapshot() =>
        new([new EvidenceSnapshot(DocumentId, ChunkId, Content)],
            new Dictionary<Guid, string> { [DocumentId] = new string('f', 64) });

    private sealed class FakeSearch : IVectorSearchService
    {
        public Task<IReadOnlyCollection<VectorSearchResultDto>> SearchAsync(
            string question, int topK = 5, CancellationToken cancellationToken = default)
        {
            IReadOnlyCollection<VectorSearchResultDto> results =
            [
                new VectorSearchResultDto
                {
                    DocumentId = DocumentId,
                    ChunkId = ChunkId,
                    Title = "Test",
                    Content = Content,
                    Score = 1.0
                }
            ];
            return Task.FromResult(results);
        }
    }

    private sealed class FakeSnapshots(bool becomesStale) : IEvidenceSnapshotProvider
    {
        private int _checks;

        public Task<EvidenceCaptureResult> CaptureAsync(
            IReadOnlyCollection<VectorSearchResultDto> retrievedChunks,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvidenceCaptureResult(EvidenceCaptureStatus.Captured, Snapshot()));

        public Task<EvidenceRevalidationStatus> RevalidateAsync(
            EvidenceRetrievalSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _checks++;
            return Task.FromResult(becomesStale && _checks > 1
                ? EvidenceRevalidationStatus.Stale
                : EvidenceRevalidationStatus.Fresh);
        }
    }

    private sealed class FakeGenerator : IClaimProposalGenerator
    {
        public Task<string?> GenerateAsync(
            string question, EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var chunk = snapshot.AllowedChunks[ChunkId];
            object NewClaim(string id, string statement) => new
            {
                id,
                statement,
                evidence = new[]
                {
                    new
                    {
                        documentId = DocumentId,
                        chunkId = ChunkId,
                        startUtf16 = 0,
                        lengthUtf16 = 6,
                        quote = "Preuve",
                        chunkSha256 = chunk.ChunkSha256
                    }
                }
            };

            var output = JsonSerializer.Serialize(new
            {
                schemaVersion = 1,
                claims = new[]
                {
                    NewClaim("c1", "Fait vérifié"),
                    NewClaim("c2", "INVENTED SECRET FACT")
                }
            });
            return Task.FromResult<string?>(output);
        }
    }

    private sealed class FakeAssessor : IQualifiedEvidenceAssessor
    {
        public Task<QualifiedEvidenceAssessment> AssessAsync(
            string question, ClaimProposalBatch proposals,
            EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var findings = proposals.Claims.SelectMany(claim => claim.Evidence.Select(pointer =>
                new SemanticEvidenceFinding(claim.Id, pointer,
                    claim.Id == "c1" ? SemanticEvidenceRelation.Entails :
                        SemanticEvidenceRelation.Unknown))).ToArray();
            return Task.FromResult(new QualifiedEvidenceAssessment(
                PolicyId, true, EvidenceCoverageStatus.Partial, findings));
        }
    }
}
