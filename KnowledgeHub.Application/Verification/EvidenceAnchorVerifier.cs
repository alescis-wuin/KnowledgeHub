namespace KnowledgeHub.Application.Verification;

public enum EvidenceAnchorStatus
{
    InvalidAnchor = 0,
    ValidAnchor = 1
}

/// <summary>
/// Verifies provenance and text identity only. This cannot establish whether
/// the quoted text entails a claim, nor whether a snapshot is still current.
/// </summary>
public static class EvidenceAnchorVerifier
{
    public static EvidenceAnchorStatus Verify(
        EvidencePointer? pointer,
        IReadOnlyDictionary<Guid, EvidenceSnapshot> allowedSnapshots)
    {
        ArgumentNullException.ThrowIfNull(allowedSnapshots);

        if (pointer is null || pointer.DocumentId == Guid.Empty ||
            pointer.ChunkId == Guid.Empty || pointer.StartUtf16 < 0 ||
            pointer.LengthUtf16 is < 1 or > ClaimProposalJsonParser.MaxQuoteUtf16 ||
            pointer.Quote is null || pointer.Quote.Length != pointer.LengthUtf16 ||
            pointer.ChunkSha256 is null || pointer.ChunkSha256.Length != 64 ||
            !allowedSnapshots.TryGetValue(pointer.ChunkId, out var snapshot) ||
            snapshot.DocumentId != pointer.DocumentId ||
            snapshot.ChunkId != pointer.ChunkId ||
            !string.Equals(snapshot.ChunkSha256, pointer.ChunkSha256, StringComparison.Ordinal))
        {
            return EvidenceAnchorStatus.InvalidAnchor;
        }

        var content = snapshot.Content;
        if (pointer.LengthUtf16 > content.Length ||
            pointer.StartUtf16 > content.Length - pointer.LengthUtf16)
        {
            return EvidenceAnchorStatus.InvalidAnchor;
        }

        var start = pointer.StartUtf16;
        var end = start + pointer.LengthUtf16; // Bounded by content.Length above.
        if (SplitsSurrogatePair(content, start) || SplitsSurrogatePair(content, end))
        {
            return EvidenceAnchorStatus.InvalidAnchor;
        }

        return content.AsSpan(start, pointer.LengthUtf16)
            .SequenceEqual(pointer.Quote.AsSpan())
            ? EvidenceAnchorStatus.ValidAnchor
            : EvidenceAnchorStatus.InvalidAnchor;
    }

    private static bool SplitsSurrogatePair(string content, int index) =>
        index > 0 && index < content.Length &&
        char.IsHighSurrogate(content[index - 1]) &&
        char.IsLowSurrogate(content[index]);
}
