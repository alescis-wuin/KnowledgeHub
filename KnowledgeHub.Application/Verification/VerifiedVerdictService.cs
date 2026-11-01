using KnowledgeHub.Application.Interfaces;

namespace KnowledgeHub.Application.Verification;

/// <summary>
/// Coordinates an independently qualified semantic assessment with the final
/// authoritative PostgreSQL freshness check. Does not call a generative LLM,
/// expose an HTTP endpoint, persist verdicts, or log private text.
/// </summary>
public sealed class VerifiedVerdictService
{
    private readonly IEvidenceSnapshotProvider _snapshotProvider;
    private readonly IQualifiedEvidenceAssessor? _assessor;
    private readonly string _trustedPolicyId;

    public VerifiedVerdictService(
        IEvidenceSnapshotProvider snapshotProvider,
        IQualifiedEvidenceAssessor? assessor,
        string trustedPolicyId)
    {
        _snapshotProvider = snapshotProvider ?? throw new ArgumentNullException(nameof(snapshotProvider));
        _assessor = assessor;
        _trustedPolicyId = trustedPolicyId ?? string.Empty;
    }

    public async Task<VerifiedEvidenceAnswer> EvaluateAsync(
        string question,
        ClaimProposalBatch? proposals,
        EvidenceRetrievalSnapshot? snapshot,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(question) || proposals is null || snapshot is null)
        {
            return VerifiedVerdictPolicy.Decide(
                proposals, snapshot, EvidenceRevalidationStatus.InvalidSnapshot,
                null, _trustedPolicyId);
        }

        var assessment = QualifiedEvidenceAssessment.Unavailable;
        if (_assessor is not null && !string.IsNullOrWhiteSpace(_trustedPolicyId))
        {
            try
            {
                assessment = await _assessor.AssessAsync(
                    question, proposals, snapshot, cancellationToken) ??
                    QualifiedEvidenceAssessment.Unavailable;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception)
            {
                // Do not leak exception messages, prompts or document text.
                assessment = QualifiedEvidenceAssessment.Unavailable;
            }
        }

        EvidenceRevalidationStatus freshness;
        try
        {
            // Revalidate AFTER potentially slow semantic assessment.
            freshness = await _snapshotProvider.RevalidateAsync(snapshot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            freshness = EvidenceRevalidationStatus.InvalidSnapshot;
        }

        return VerifiedVerdictPolicy.Decide(
            proposals, snapshot, freshness, assessment, _trustedPolicyId);
    }
}
