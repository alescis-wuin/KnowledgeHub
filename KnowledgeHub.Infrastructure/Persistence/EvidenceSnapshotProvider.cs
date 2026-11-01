using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Verification;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence;

/// <summary>
/// Reads the authoritative DB, not an in-memory vector-index cache, for a
/// bounded set of chunk IDs already selected by the server's retrieval path.
/// Capture and revalidation each use a single PostgreSQL statement snapshot.
/// No long-lived database lock is held between them (KH-008.3 must revalidate).
/// </summary>
public sealed class EvidenceSnapshotProvider : IEvidenceSnapshotProvider
{
    private const int MaxSelectedChunks = 32;
    private readonly KnowledgeDbContext _context;

    public EvidenceSnapshotProvider(KnowledgeDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<EvidenceCaptureResult> CaptureAsync(
        IReadOnlyCollection<VectorSearchResultDto> retrievedChunks,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(retrievedChunks);
        cancellationToken.ThrowIfCancellationRequested();

        if (retrievedChunks.Count == 0)
        {
            return new EvidenceCaptureResult(EvidenceCaptureStatus.EmptyRetrieval, null);
        }

        if (retrievedChunks.Count > MaxSelectedChunks || retrievedChunks.Any(chunk =>
                chunk is null || chunk.ChunkId == Guid.Empty ||
                chunk.DocumentId == Guid.Empty || chunk.Content is null) ||
            retrievedChunks.Select(chunk => chunk.ChunkId).Distinct().Count() !=
                retrievedChunks.Count)
        {
            return new EvidenceCaptureResult(EvidenceCaptureStatus.InvalidRetrieval, null);
        }

        // Freeze the mutable retrieval DTOs before the first asynchronous read.
        // A later mutation of the vector-search result cannot rewrite authority.
        var selected = retrievedChunks.ToDictionary(
            chunk => chunk.ChunkId,
            chunk => (chunk.DocumentId, chunk.Content));
        var rows = await ReadAuthoritativeAsync(selected.Keys.ToArray(), cancellationToken);
        if (rows.Count != selected.Count)
        {
            return new EvidenceCaptureResult(EvidenceCaptureStatus.StaleRetrieval, null);
        }

        var snapshots = new List<EvidenceSnapshot>(rows.Count);
        var documentVersions = new Dictionary<Guid, string>();

        foreach (var row in rows)
        {
            if (!selected.TryGetValue(row.ChunkId, out var selectedChunk) ||
                row.DocumentId != selectedChunk.DocumentId ||
                !string.Equals(row.ChunkContent, selectedChunk.Content, StringComparison.Ordinal))
            {
                return new EvidenceCaptureResult(EvidenceCaptureStatus.StaleRetrieval, null);
            }

            if (!TryComputeDocumentVersion(row, out var version))
            {
                return new EvidenceCaptureResult(EvidenceCaptureStatus.InconsistentSource, null);
            }

            if (documentVersions.TryGetValue(row.DocumentId, out var previous) &&
                !string.Equals(previous, version, StringComparison.Ordinal))
            {
                return new EvidenceCaptureResult(EvidenceCaptureStatus.InconsistentSource, null);
            }

            documentVersions[row.DocumentId] = version;
            snapshots.Add(new EvidenceSnapshot(row.DocumentId, row.ChunkId, row.ChunkContent));
        }

        return new EvidenceCaptureResult(
            EvidenceCaptureStatus.Captured,
            new EvidenceRetrievalSnapshot(snapshots, documentVersions));
    }

    public async Task<EvidenceRevalidationStatus> RevalidateAsync(
        EvidenceRetrievalSnapshot snapshot,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        cancellationToken.ThrowIfCancellationRequested();

        var selected = snapshot.AllowedChunks;
        if (selected.Count == 0 || selected.Count > MaxSelectedChunks)
        {
            return EvidenceRevalidationStatus.InvalidSnapshot;
        }

        var rows = await ReadAuthoritativeAsync(selected.Keys.ToArray(), cancellationToken);
        if (rows.Count != selected.Count)
        {
            return EvidenceRevalidationStatus.Stale;
        }

        foreach (var row in rows)
        {
            if (!selected.TryGetValue(row.ChunkId, out var captured) ||
                !snapshot.DocumentContentSha256.TryGetValue(row.DocumentId, out var originalDocHash) ||
                captured.DocumentId != row.DocumentId ||
                !string.Equals(captured.Content, row.ChunkContent, StringComparison.Ordinal) ||
                !TryComputeDocumentVersion(row, out var currentDocHash) ||
                !string.Equals(currentDocHash, originalDocHash, StringComparison.Ordinal))
            {
                return EvidenceRevalidationStatus.Stale;
            }
        }

        return EvidenceRevalidationStatus.Fresh;
    }

    private async Task<List<EvidenceRow>> ReadAuthoritativeAsync(
        Guid[] selectedChunkIds,
        CancellationToken cancellationToken)
    {
        // A single SQL statement sees one coherent DB snapshot, even if an
        // indexed document is being replaced in a concurrent transaction.
        return await _context.DocumentChunks
            .AsNoTracking()
            .Where(chunk => selectedChunkIds.Contains(chunk.Id))
            .Select(chunk => new EvidenceRow(
                chunk.Id,
                chunk.DocumentId,
                chunk.Content,
                chunk.Document.Content,
                chunk.Document.ContentSha256))
            .ToListAsync(cancellationToken);
    }

    private static bool TryComputeDocumentVersion(EvidenceRow row, out string version)
    {
        version = string.Empty;
        if (row.DocumentId == Guid.Empty || row.ChunkId == Guid.Empty ||
            string.IsNullOrWhiteSpace(row.DocumentContent) || row.ChunkContent is null)
        {
            return false;
        }

        // Legacy rows may contain NULL in ContentSha256. Always recompute from
        // canonical authoritative document bytes; never trust a model-provided
        // digest or replace the document digest with the chunk digest.
        version = KnowledgeDocument.ComputeContentSha256(row.DocumentContent);
        return row.StoredDocumentSha256 is null ||
            string.Equals(row.StoredDocumentSha256, version, StringComparison.Ordinal);
    }

    private sealed record EvidenceRow(
        Guid ChunkId,
        Guid DocumentId,
        string ChunkContent,
        string DocumentContent,
        string? StoredDocumentSha256);
}
