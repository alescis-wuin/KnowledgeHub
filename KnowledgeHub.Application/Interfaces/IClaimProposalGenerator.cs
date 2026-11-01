using KnowledgeHub.Application.Verification;

namespace KnowledgeHub.Application.Interfaces;

/// <summary>
/// Generates untrusted candidate claims. The output is never a verdict.
/// Returns only the raw model content, which must be parsed with bounded rules.
/// </summary>
public interface IClaimProposalGenerator
{
    Task<string?> GenerateAsync(
        string question,
        EvidenceRetrievalSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
