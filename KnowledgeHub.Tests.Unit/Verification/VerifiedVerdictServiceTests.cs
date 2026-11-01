using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Verification;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Verification;

public sealed class VerifiedVerdictServiceTests
{
    private const string Policy = "trusted-test-policy";

    [Fact]
    public async Task RevalidatesAfterSemanticAssessmentBeforePublishing()
    {
        var (batch, snapshot) = Fixture();
        var order = new List<string>();
        var provider = new TestProvider(() =>
        {
            order.Add("db-freshness");
            return EvidenceRevalidationStatus.Fresh;
        });
        var assessor = new TestAssessor(batch, () => order.Add("semantic"));
        var service = new VerifiedVerdictService(provider, assessor, Policy);
        var result = await service.EvaluateAsync("question", batch, snapshot);
        Assert.Equal(VerifiedAnswerStatus.Answer, result.Status);
        Assert.Equal(new[] { "semantic", "db-freshness" }, order);
    }

    [Fact]
    public async Task RejectsConcurrentMutationEvenIfAssessmentEntails()
    {
        var (batch, snapshot) = Fixture();
        var provider = new TestProvider(() => EvidenceRevalidationStatus.Stale);
        var result = await new VerifiedVerdictService(
            provider, new TestAssessor(batch), Policy)
            .EvaluateAsync("question", batch, snapshot);
        Assert.Equal(VerifiedAnswerStatus.Abstain, result.Status);
    }

    [Fact]
    public async Task AssessorFailureOrUnavailabilityAbstains()
    {
        var (batch, snapshot) = Fixture();
        var provider = new TestProvider(() => EvidenceRevalidationStatus.Fresh);
        var missing = await new VerifiedVerdictService(provider, null, Policy)
            .EvaluateAsync("question", batch, snapshot);
        Assert.Equal(VerifiedAnswerStatus.Abstain, missing.Status);
        var failed = await new VerifiedVerdictService(
            provider, new TestAssessor(batch, throwException: true), Policy)
            .EvaluateAsync("question", batch, snapshot);
        Assert.Equal(VerifiedAnswerStatus.Abstain, failed.Status);
    }

    [Fact]
    public async Task DatabaseFailureAbstainsWithoutReturningPrivateException()
    {
        var (batch, snapshot) = Fixture();
        var provider = new TestProvider(() => throw new InvalidOperationException("private corpus sentinel"));
        var result = await new VerifiedVerdictService(provider, new TestAssessor(batch), Policy)
            .EvaluateAsync("question", batch, snapshot);
        Assert.Equal(VerifiedAnswerStatus.Abstain, result.Status);
        Assert.DoesNotContain("private corpus sentinel", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationIsPropagatedWithoutPublishing()
    {
        var (batch, snapshot) = Fixture();
        var provider = new TestProvider(() => EvidenceRevalidationStatus.Fresh);
        using var source = new CancellationTokenSource();
        source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new VerifiedVerdictService(provider, new TestAssessor(batch), Policy)
                .EvaluateAsync("question", batch, snapshot, source.Token));
    }

    private static (ClaimProposalBatch, EvidenceRetrievalSnapshot) Fixture()
    {
        var documentId = Guid.Parse("11111111-1111-4111-8111-111111111111");
        var chunkId = Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa");
        var chunk = new EvidenceSnapshot(documentId, chunkId, "Preuve exacte");
        var snapshot = new EvidenceRetrievalSnapshot(
            [chunk], new Dictionary<Guid, string> { [documentId] = new string('0', 64) });
        var pointer = new EvidencePointer(documentId, chunkId, 0, 6, "Preuve", chunk.ChunkSha256);
        var batch = new ClaimProposalBatch(1, [new ClaimProposal("c1", "Affirmation", [pointer])]);
        return (batch, snapshot);
    }

    private sealed class TestProvider(Func<EvidenceRevalidationStatus> result) : IEvidenceSnapshotProvider
    {
        public Task<EvidenceCaptureResult> CaptureAsync(
            IReadOnlyCollection<VectorSearchResultDto> retrievedChunks,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new EvidenceCaptureResult(EvidenceCaptureStatus.EmptyRetrieval, null));

        public Task<EvidenceRevalidationStatus> RevalidateAsync(
            EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default) => Task.FromResult(result());
    }

    private sealed class TestAssessor(
        ClaimProposalBatch batch,
        Action? callback = null,
        bool throwException = false) : IQualifiedEvidenceAssessor
    {
        public Task<QualifiedEvidenceAssessment> AssessAsync(
            string question,
            ClaimProposalBatch proposals,
            EvidenceRetrievalSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            callback?.Invoke();
            if (throwException)
            {
                throw new InvalidOperationException("private model sentinel");
            }

            var findings = batch.Claims.SelectMany(claim => claim.Evidence.Select(pointer =>
                new SemanticEvidenceFinding(claim.Id, pointer, SemanticEvidenceRelation.Entails))).ToArray();
            return Task.FromResult(new QualifiedEvidenceAssessment(
                Policy, true, EvidenceCoverageStatus.Complete, findings));
        }
    }
}
