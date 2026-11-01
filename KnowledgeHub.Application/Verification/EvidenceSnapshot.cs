using System.Security.Cryptography;
using System.Text;

namespace KnowledgeHub.Application.Verification;

/// <summary>
/// A server-owned snapshot of one allowed chunk. Freshness against the database
/// must be checked by the caller (KH-008.2) before using any verdict.
/// </summary>
public sealed class EvidenceSnapshot
{
    public Guid DocumentId { get; }
    public Guid ChunkId { get; }
    public string Content { get; }
    public string ChunkSha256 { get; }

    public EvidenceSnapshot(Guid documentId, Guid chunkId, string content)
    {
        if (documentId == Guid.Empty || chunkId == Guid.Empty)
        {
            throw new ArgumentException("A nonempty document and chunk identity is required.");
        }

        ArgumentNullException.ThrowIfNull(content);
        DocumentId = documentId;
        ChunkId = chunkId;
        Content = content;
        ChunkSha256 = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content)))
            .ToLowerInvariant();
    }
}
