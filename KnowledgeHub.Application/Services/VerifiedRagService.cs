using System.Text;
using KnowledgeHub.Application.DTOs;
using KnowledgeHub.Application.Interfaces;
using KnowledgeHub.Application.Verification;

namespace KnowledgeHub.Application.Services;

/// <summary>
/// Dedicated, fail-closed verified RAG pipeline. The legacy RagService remains
/// untouched. Without a separately configured semantic assessor, no retrieval
/// or generative inference is performed and the response abstains.
/// </summary>
public sealed class VerifiedRagService
{
    private const int MaxQuestionUtf16 = 4096;
    private const int RetrievalTopK = 5;

    private readonly IVectorSearchService _search;
    private readonly IEvidenceSnapshotProvider _snapshots;
    private readonly IClaimProposalGenerator _generator;
    private readonly VerifiedVerdictService _verdict;
    private readonly bool _assessorConfigured;

    public VerifiedRagService(
        IVectorSearchService search,
        IEvidenceSnapshotProvider snapshots,
        IClaimProposalGenerator generator,
        VerifiedVerdictService verdict,
        bool assessorConfigured)
    {
        _search = search ?? throw new ArgumentNullException(nameof(search));
        _snapshots = snapshots ?? throw new ArgumentNullException(nameof(snapshots));
        _generator = generator ?? throw new ArgumentNullException(nameof(generator));
        _verdict = verdict ?? throw new ArgumentNullException(nameof(verdict));
        _assessorConfigured = assessorConfigured;
    }

    public async Task<VerifiedRagResponseDto> AskAsync(
        string? question,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!_assessorConfigured || string.IsNullOrWhiteSpace(question) ||
            question.Length > MaxQuestionUtf16)
        {
            return VerifiedRagResponseDto.Abstain();
        }

        try
        {
            var retrieved = await _search.SearchAsync(
                question, RetrievalTopK, cancellationToken);
            if (retrieved is null || retrieved.Count == 0)
            {
                return VerifiedRagResponseDto.Abstain();
            }

            var capture = await _snapshots.CaptureAsync(retrieved, cancellationToken);
            if (capture.Status != EvidenceCaptureStatus.Captured ||
                capture.Snapshot is null)
            {
                return VerifiedRagResponseDto.Abstain();
            }

            // Construct the model prompt from server-authorized persisted bytes,
            // never from untrusted retrieval text or model-supplied source IDs.
            var raw = await _generator.GenerateAsync(
                question, capture.Snapshot, cancellationToken);
            if (string.IsNullOrWhiteSpace(raw))
            {
                return VerifiedRagResponseDto.Abstain();
            }

            var jsonBytes = Encoding.UTF8.GetBytes(raw);
            if (ClaimProposalJsonParser.TryParse(jsonBytes, out var batch) !=
                ClaimProposalParseStatus.Valid || batch is null)
            {
                return VerifiedRagResponseDto.Abstain();
            }

            var decision = await _verdict.EvaluateAsync(
                question, batch, capture.Snapshot, cancellationToken);

            // Additional last-moment check before the public response is built.
            // This narrows the TOCTOU window but does not make HTTP emission
            // atomic with PostgreSQL mutations.
            if (decision.Status is VerifiedAnswerStatus.Answer or
                VerifiedAnswerStatus.Partial or VerifiedAnswerStatus.Conflict)
            {
                var freshness = await _snapshots.RevalidateAsync(
                    capture.Snapshot, cancellationToken);
                if (freshness != EvidenceRevalidationStatus.Fresh)
                {
                    return VerifiedRagResponseDto.Abstain();
                }
            }

            return VerifiedRagResponseDto.FromVerifiedDecision(decision);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception)
        {
            // Never expose exception messages, prompts or private document text.
            return VerifiedRagResponseDto.Abstain();
        }
    }
}
