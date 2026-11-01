using KnowledgeHub.Application.Verification;

namespace KnowledgeHub.Application.Interfaces;

/// <summary>
/// A separately configured, independently qualified semantic verifier AND
/// question-coverage assessor. Implementations must never accept an LLM's
/// proposed verdict as authority. No qualified implementation is installed
/// by KH-008.3; without one, the decision must abstain.
/// </summary>
public interface IQualifiedEvidenceAssessor
{
    Task<QualifiedEvidenceAssessment> AssessAsync(
        string question,
        ClaimProposalBatch proposals,
        EvidenceRetrievalSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
