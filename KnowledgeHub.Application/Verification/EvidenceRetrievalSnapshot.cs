using System.Collections.ObjectModel;

namespace KnowledgeHub.Application.Verification;

/// <summary>
/// A closed allowlist of authoritative chunk bytes and document-content versions.
/// Holding this object does not prove that the persisted contents are still current:
/// the caller must revalidate before any decision (KH-008.3).
/// </summary>
public sealed class EvidenceRetrievalSnapshot
{
    private readonly ReadOnlyDictionary<Guid, EvidenceSnapshot> _chunks;
    private readonly ReadOnlyDictionary<Guid, string> _documentContentSha256;

    public IReadOnlyDictionary<Guid, EvidenceSnapshot> AllowedChunks => _chunks;
    public IReadOnlyDictionary<Guid, string> DocumentContentSha256 => _documentContentSha256;

    public EvidenceRetrievalSnapshot(
        IReadOnlyCollection<EvidenceSnapshot> chunks,
        IReadOnlyDictionary<Guid, string> documentContentSha256)
    {
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(documentContentSha256);

        if (chunks.Count == 0 || chunks.Count > 32)
        {
            throw new ArgumentException("The evidence allowlist must contain 1 to 32 chunks.", nameof(chunks));
        }

        var selected = new Dictionary<Guid, EvidenceSnapshot>();
        foreach (var chunk in chunks)
        {
            if (chunk is null || !selected.TryAdd(chunk.ChunkId, chunk) ||
                !documentContentSha256.TryGetValue(chunk.DocumentId, out var digest) ||
                !IsLowerHexSha256(digest))
            {
                throw new ArgumentException("The evidence allowlist is inconsistent.", nameof(chunks));
            }
        }

        var versions = new Dictionary<Guid, string>();
        foreach (var pair in documentContentSha256)
        {
            if (pair.Key == Guid.Empty || !IsLowerHexSha256(pair.Value) ||
                !selected.Values.Any(chunk => chunk.DocumentId == pair.Key))
            {
                throw new ArgumentException("The document versions are inconsistent.", nameof(documentContentSha256));
            }

            versions.Add(pair.Key, pair.Value);
        }

        _chunks = new ReadOnlyDictionary<Guid, EvidenceSnapshot>(selected);
        _documentContentSha256 = new ReadOnlyDictionary<Guid, string>(versions);
    }

    private static bool IsLowerHexSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public enum EvidenceCaptureStatus
{
    Captured = 0,
    EmptyRetrieval = 1,
    InvalidRetrieval = 2,
    StaleRetrieval = 3,
    InconsistentSource = 4
}

public sealed record EvidenceCaptureResult(
    EvidenceCaptureStatus Status,
    EvidenceRetrievalSnapshot? Snapshot);

public enum EvidenceRevalidationStatus
{
    Fresh = 0,
    Stale = 1,
    InvalidSnapshot = 2
}
