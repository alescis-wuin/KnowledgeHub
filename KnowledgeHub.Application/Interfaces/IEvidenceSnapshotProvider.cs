using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Verification;

namespace KnowledgeHub.Application.Interfaces;

/// <summary>
/// Captures server-authorized persisted chunks and validates their current
/// content versions. Call RevalidateAsync immediately before a verified verdict.
/// This interface never assesses semantic entailment.
/// </summary>
public interface IEvidenceSnapshotProvider
{
    Task<EvidenceCaptureResult> CaptureAsync(
        IReadOnlyCollection<VectorSearchResultDto> retrievedChunks,
        CancellationToken cancellationToken = default);

    Task<EvidenceRevalidationStatus> RevalidateAsync(
        EvidenceRetrievalSnapshot snapshot,
        CancellationToken cancellationToken = default);
}
