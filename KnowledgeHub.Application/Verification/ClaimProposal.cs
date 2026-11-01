namespace KnowledgeHub.Application.Verification;

/// <summary>
/// An untrusted model assertion. It is never evidence of semantic entailment.
/// </summary>
public sealed record ClaimProposal(
    string Id,
    string Statement,
    IReadOnlyList<EvidencePointer> Evidence);

/// <summary>
/// References an exact UTF-16 slice of a chunk authorized for this request.
/// </summary>
public sealed record EvidencePointer(
    Guid DocumentId,
    Guid ChunkId,
    int StartUtf16,
    int LengthUtf16,
    string Quote,
    string ChunkSha256);

/// <summary>
/// An untrusted collection of atomic assertions returned by a model.
/// </summary>
public sealed record ClaimProposalBatch(
    int SchemaVersion,
    IReadOnlyList<ClaimProposal> Claims);
