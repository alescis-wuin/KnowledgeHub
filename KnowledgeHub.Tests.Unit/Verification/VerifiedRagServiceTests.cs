using System.Text.Json;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Services;
using KnowledgeHub.Application.Verification;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Verification;

public sealed class VerifiedRagServiceTests
{
    private const string Policy = "test-qualified-v1";
    private static readonly Guid DocumentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ChunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
    private static readonly EvidenceSnapshot Chunk = new(DocumentId, ChunkId, "Texte exact pour la preuve.");

    private static EvidenceRetrievalSnapshot Snapshot() =>
        new([Chunk], new Dictionary<Guid, string> { [DocumentId] = new string('a', 64) });

    private static string Candidate(bool withRejected = false, bool forged = false)
    {
        object Build(string id, string statement) => new
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
                    lengthUtf16 = 11,
                    quote = forged ? "Texte FAUX!" : "Texte exact",
                    chunkSha256 = Chunk.ChunkSha256
                }
            }
        };
        var claims = new List<object> { Build("c1", "Fait approuvé") };
        if (withRejected)
        {
            claims.Add(Build("c2", "FAIT PRIVE INVENTE"));
        }

        return JsonSerializer.Serialize(new { schemaVersion = 1, claims });
    }

    private static VerifiedRagService Service(
        TestSearch search, TestSnapshots provider, TestGenerator generator,
        bool configured = true, IQualifiedEvidenceAssessor? assessor = null) =>
        new(search, provider, generator,
            new VerifiedVerdictService(provider, assessor, Policy), configured);

    [Fact]
    public async Task MissingAssessorConfigurationSkipsRetrievalAndOllama()
    {
        var search = new TestSearch();
        var provider = new TestSnapshots(Snapshot());
        var generator = new TestGenerator(Candidate());
        var result = await Service(search, provider, generator, configured: false)
            .AskAsync("question");
        Assert.Equal("ABSTAIN", result.Verdict);
        Assert.Empty(result.Claims);
        Assert.Equal(0, search.Calls);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task PublicDtoNeverIncludesUnapprovedClaim()
    {
        var provider = new TestSnapshots(Snapshot());
        var result = await Service(new TestSearch(), provider,
            new TestGenerator(Candidate(withRejected: true)), assessor: new TestAssessor())
            .AskAsync("question");
        Assert.Equal("PARTIAL", result.Verdict);
        Assert.Equal("Fait approuvé", Assert.Single(result.Claims).Statement);
        Assert.Equal(2, provider.Revalidations);
        Assert.DoesNotContain("FAIT PRIVE INVENTE", JsonSerializer.Serialize(result),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConcurrentMutationAfterFirstFreshCheckBlocksPublication()
    {
        var provider = new TestSnapshots(Snapshot())
        {
            Freshness = call => call == 1
                ? EvidenceRevalidationStatus.Fresh
                : EvidenceRevalidationStatus.Stale
        };
        var response = await Service(new TestSearch(), provider,
            new TestGenerator(Candidate()), assessor: new TestAssessor())
            .AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.Empty(response.Claims);
        Assert.Equal(2, provider.Revalidations);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("{\"schemaVersion\":1,\"claims\":[]}")]
    [InlineData("{\"schemaVersion\":1,\"claims\":[{\"id\":\"c1\"}]}")]
    public async Task MalformedModelOutputAbstains(string raw)
    {
        var response = await Service(new TestSearch(), new TestSnapshots(Snapshot()),
            new TestGenerator(raw), assessor: new TestAssessor()).AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.Empty(response.Claims);
    }

    [Fact]
    public async Task FabricatedQuoteAbstainsWithoutPublishingClaim()
    {
        var response = await Service(new TestSearch(), new TestSnapshots(Snapshot()),
            new TestGenerator(Candidate(forged: true)), assessor: new TestAssessor())
            .AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.DoesNotContain("Fait approuvé", JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(EvidenceCaptureStatus.StaleRetrieval)]
    [InlineData(EvidenceCaptureStatus.InconsistentSource)]
    [InlineData(EvidenceCaptureStatus.InvalidRetrieval)]
    public async Task InvalidOrStaleCaptureNeverInvokesGenerator(EvidenceCaptureStatus status)
    {
        var provider = new TestSnapshots(Snapshot()) { CaptureStatus = status };
        var generator = new TestGenerator(Candidate());
        var response = await Service(new TestSearch(), provider, generator,
            assessor: new TestAssessor()).AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.Equal(0, generator.Calls);
    }

    [Fact]
    public async Task UnknownCoverageAbstains()
    {
        var response = await Service(new TestSearch(), new TestSnapshots(Snapshot()),
            new TestGenerator(Candidate()),
            assessor: new TestAssessor(coverage: EvidenceCoverageStatus.Unknown))
            .AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.Empty(response.Claims);
    }

    [Fact]
    public async Task ConflictingEvidenceDoesNotExposeClaims()
    {
        var response = await Service(new TestSearch(), new TestSnapshots(Snapshot()),
            new TestGenerator(Candidate()),
            assessor: new TestAssessor(relation: SemanticEvidenceRelation.Conflicts))
            .AskAsync("question");
        Assert.Equal("CONFLICT", response.Verdict);
        Assert.Empty(response.Claims);
        Assert.DoesNotContain("Fait approuvé", JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task PrivateFailureIsNotReflectedInHttpResponse()
    {
        var response = await Service(new TestSearch { Fail = true },
            new TestSnapshots(Snapshot()), new TestGenerator(Candidate()),
            assessor: new TestAssessor()).AskAsync("question");
        Assert.Equal("ABSTAIN", response.Verdict);
        Assert.DoesNotContain("CONFIDENTIAL_SENTINEL", JsonSerializer.Serialize(response),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationIsPropagated()
    {
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Service(new TestSearch(), new TestSnapshots(Snapshot()),
                new TestGenerator(Candidate()), assessor: new TestAssessor())
                .AskAsync("question", source.Token));
    }

    private sealed class TestSearch : IVectorSearchService
    {
        public int Calls { get; private set; }
        public bool Fail { get; set; }

        public Task<IReadOnlyCollection<VectorSearchResultDto>> SearchAsync(
            string question, int topK = 5, CancellationToken cancellationToken = default)
        {
            Calls++;
            if (Fail)
            {
                throw new InvalidOperationException("CONFIDENTIAL_SENTINEL");
            }

            IReadOnlyCollection<VectorSearchResultDto> result =
            [
                new VectorSearchResultDto
                {
                    DocumentId = DocumentId,
                    ChunkId = ChunkId,
                    Title = "Titre",
                    Content = Chunk.Content,
                    Score = 1.0
                }
            ];
            return Task.FromResult(result);
        }
    }

    private sealed class TestSnapshots(EvidenceRetrievalSnapshot snapshot) : IEvidenceSnapshotProvider
    {
        public int Revalidations { get; private set; }
        public EvidenceCaptureStatus CaptureStatus { get; set; } = EvidenceCaptureStatus.Captured;
        public Func<int, EvidenceRevalidationStatus> Freshness { get; set; } =
            _ => EvidenceRevalidationStatus.Fresh;

        public Task<EvidenceCaptureResult> CaptureAsync(
            IReadOnlyCollection<VectorSearchResultDto> retrievedChunks,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvidenceCaptureResult(CaptureStatus,
                CaptureStatus == EvidenceCaptureStatus.Captured ? snapshot : null));

        public Task<EvidenceRevalidationStatus> RevalidateAsync(
            EvidenceRetrievalSnapshot value,
            CancellationToken cancellationToken = default)
        {
            Revalidations++;
            return Task.FromResult(Freshness(Revalidations));
        }
    }

    private sealed class TestGenerator(string? response) : IClaimProposalGenerator
    {
        public int Calls { get; private set; }

        public Task<string?> GenerateAsync(
            string question, EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(response);
        }
    }

    private sealed class TestAssessor(
        SemanticEvidenceRelation relation = SemanticEvidenceRelation.Entails,
        EvidenceCoverageStatus coverage = EvidenceCoverageStatus.Complete) : IQualifiedEvidenceAssessor
    {
        public Task<QualifiedEvidenceAssessment> AssessAsync(
            string question, ClaimProposalBatch proposals,
            EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            var findings = proposals.Claims.SelectMany(claim => claim.Evidence.Select(pointer =>
                new SemanticEvidenceFinding(claim.Id, pointer,
                    claim.Id == "c2" ? SemanticEvidenceRelation.Unknown : relation))).ToArray();
            return Task.FromResult(new QualifiedEvidenceAssessment(Policy, true, coverage, findings));
        }
    }
}
