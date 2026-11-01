namespace KnowledgeHub.Application.Verification;

/// <summary>
/// An independent qualification result, not a verdict or score from the
/// claim-proposing LLM. Only an explicitly configured policy may be trusted.
/// </summary>
public enum SemanticEvidenceRelation
{
    Unknown = 0,
    Entails = 1,
    Contradicts = 2,
    Conflicts = 3
}

public enum EvidenceCoverageStatus
{
    Unknown = 0,
    Complete = 1,
    Partial = 2
}

/// <summary>
/// Binds a semantic judgement to an entire exact evidence pointer, including
/// its source identity, offset, text and server-computed digest.
/// </summary>
public sealed record SemanticEvidenceFinding(
    string ClaimId,
    EvidencePointer Evidence,
    SemanticEvidenceRelation Relation);

public sealed record QualifiedEvidenceAssessment(
    string PolicyId,
    bool Qualified,
    EvidenceCoverageStatus Coverage,
    IReadOnlyList<SemanticEvidenceFinding> Findings)
{
    public static QualifiedEvidenceAssessment Unavailable { get; } = new(
        string.Empty,
        false,
        EvidenceCoverageStatus.Unknown,
        []);
}

public enum VerifiedClaimStatus
{
    Supported = 0,
    InsufficientEvidence = 1,
    InvalidEvidence = 2,
    Contradicted = 3,
    ConflictingEvidence = 4
}

public enum VerifiedAnswerStatus
{
    Abstain = 0,
    Answer = 1,
    Partial = 2,
    Conflict = 3
}

public sealed record VerifiedClaimResult(
    string Id,
    VerifiedClaimStatus Status,
    string Statement,
    IReadOnlyList<EvidencePointer> Evidence);

/// <summary>
/// Only Supported claims may appear as facts in Text. No free-form LLM output
/// is used as a final answer. All statuses are computed in C#.
/// </summary>
public sealed record VerifiedEvidenceAnswer(
    VerifiedAnswerStatus Status,
    IReadOnlyList<VerifiedClaimResult> Claims,
    string Text);
