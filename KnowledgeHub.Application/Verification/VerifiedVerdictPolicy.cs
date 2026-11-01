using System.Text;

namespace KnowledgeHub.Application.Verification;

/// <summary>
/// A pure, deterministic and fail-closed C# verdict. Exact anchors only prove
/// provenance; qualified independent assessments are required for entailment.
/// A fresh DB check must be supplied by the service immediately before this
/// decision, and the producer must not publish after another DB change.
/// </summary>
public static class VerifiedVerdictPolicy
{
    public static VerifiedEvidenceAnswer Decide(
        ClaimProposalBatch? proposals,
        EvidenceRetrievalSnapshot? snapshot,
        EvidenceRevalidationStatus freshness,
        QualifiedEvidenceAssessment? assessment,
        string trustedPolicyId)
    {
        if (proposals is null || snapshot is null ||
            freshness != EvidenceRevalidationStatus.Fresh ||
            string.IsNullOrWhiteSpace(trustedPolicyId) ||
            !ValidProposalShape(proposals))
        {
            return Abstain();
        }

        // Do not salvage a partially corrupted batch. No semantic result may
        // excuse a fabricated quote or an unauthorised source.
        foreach (var claim in proposals.Claims)
        {
            foreach (var pointer in claim.Evidence)
            {
                if (EvidenceAnchorVerifier.Verify(pointer, snapshot.AllowedChunks) !=
                    EvidenceAnchorStatus.ValidAnchor)
                {
                    return Abstain(proposals.Claims.Select(item =>
                        new VerifiedClaimResult(item.Id, VerifiedClaimStatus.InvalidEvidence,
                            item.Statement, [])).ToArray());
                }
            }
        }

        // A qualified flag supplied in the same answer as a model proposal is
        // not authority: callers must provide assessments from the separate
        // configured IQualifiedEvidenceAssessor, with an expected policy ID.
        if (!ValidAssessment(assessment, proposals, trustedPolicyId))
        {
            return Abstain(proposals.Claims.Select(item =>
                new VerifiedClaimResult(item.Id, VerifiedClaimStatus.InsufficientEvidence,
                    item.Statement, [])).ToArray());
        }

        var results = new List<VerifiedClaimResult>();
        foreach (var claim in proposals.Claims)
        {
            var relations = assessment!.Findings
                .Where(finding => string.Equals(finding.ClaimId, claim.Id, StringComparison.Ordinal))
                .Select(finding => finding.Relation)
                .ToArray();

            var entails = relations.Contains(SemanticEvidenceRelation.Entails);
            var contradicts = relations.Contains(SemanticEvidenceRelation.Contradicts);
            var conflicting = relations.Contains(SemanticEvidenceRelation.Conflicts) ||
                entails && contradicts;

            var status = conflicting
                ? VerifiedClaimStatus.ConflictingEvidence
                : contradicts
                    ? VerifiedClaimStatus.Contradicted
                    : relations.All(relation => relation == SemanticEvidenceRelation.Entails)
                        ? VerifiedClaimStatus.Supported
                        : VerifiedClaimStatus.InsufficientEvidence;

            results.Add(new VerifiedClaimResult(
                claim.Id, status, claim.Statement,
                status == VerifiedClaimStatus.Supported ? claim.Evidence.ToArray() : []));
        }

        if (results.Any(result => result.Status == VerifiedClaimStatus.ConflictingEvidence))
        {
            return new VerifiedEvidenceAnswer(VerifiedAnswerStatus.Conflict, results,
                "Conflit documentaire non résolu. Aucune conclusion factuelle publiée.");
        }

        // Unknown coverage must never silently become an ANSWER or PARTIAL.
        if (assessment!.Coverage == EvidenceCoverageStatus.Unknown)
        {
            return new VerifiedEvidenceAnswer(VerifiedAnswerStatus.Abstain, results, AbstentionText);
        }

        var supported = results.Where(result => result.Status == VerifiedClaimStatus.Supported).ToArray();
        if (supported.Length == 0)
        {
            return new VerifiedEvidenceAnswer(VerifiedAnswerStatus.Abstain, results, AbstentionText);
        }

        var answerStatus = supported.Length == results.Count &&
            assessment.Coverage == EvidenceCoverageStatus.Complete
            ? VerifiedAnswerStatus.Answer
            : VerifiedAnswerStatus.Partial;

        return new VerifiedEvidenceAnswer(answerStatus, results, Render(supported, answerStatus));
    }

    private const string AbstentionText =
        "Preuves insuffisantes ou validation non disponible : aucune réponse factuelle publiée.";

    private static VerifiedEvidenceAnswer Abstain(
        IReadOnlyList<VerifiedClaimResult>? claims = null) =>
        new(VerifiedAnswerStatus.Abstain, claims ?? [], AbstentionText);

    private static bool ValidProposalShape(ClaimProposalBatch batch)
    {
        if (batch.SchemaVersion != 1 || batch.Claims is null ||
            batch.Claims.Count is < 1 or > ClaimProposalJsonParser.MaxClaims)
        {
            return false;
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var claim in batch.Claims)
        {
            if (claim is null || string.IsNullOrWhiteSpace(claim.Id) ||
                claim.Id.Length > 32 ||
                !claim.Id.All(character => character is >= 'a' and <= 'z' or
                    >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-') ||
                !ids.Add(claim.Id) ||
                string.IsNullOrWhiteSpace(claim.Statement) ||
                claim.Statement.Length > ClaimProposalJsonParser.MaxStatementUtf16 ||
                claim.Evidence is null ||
                claim.Evidence.Count is < 1 or > ClaimProposalJsonParser.MaxEvidencePerClaim)
            {
                return false;
            }

            var identities = new HashSet<(Guid, Guid, int, int)>();
            foreach (var pointer in claim.Evidence)
            {
                if (pointer is null ||
                    !identities.Add((pointer.DocumentId, pointer.ChunkId,
                        pointer.StartUtf16, pointer.LengthUtf16)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static bool ValidAssessment(
        QualifiedEvidenceAssessment? assessment,
        ClaimProposalBatch batch,
        string trustedPolicyId)
    {
        if (assessment is null || !assessment.Qualified ||
            !string.Equals(assessment.PolicyId, trustedPolicyId, StringComparison.Ordinal) ||
            assessment.Coverage is < EvidenceCoverageStatus.Unknown or > EvidenceCoverageStatus.Partial ||
            assessment.Findings is null)
        {
            return false;
        }

        var expected = new HashSet<(string, EvidencePointer)>();
        foreach (var claim in batch.Claims)
        {
            foreach (var evidence in claim.Evidence)
            {
                expected.Add((claim.Id, evidence));
            }
        }

        if (expected.Count != assessment.Findings.Count)
        {
            return false;
        }

        var found = new HashSet<(string, EvidencePointer)>();
        foreach (var finding in assessment.Findings)
        {
            if (finding is null || finding.Evidence is null ||
                !Enum.IsDefined(finding.Relation) ||
                !expected.Contains((finding.ClaimId, finding.Evidence)) ||
                !found.Add((finding.ClaimId, finding.Evidence)))
            {
                return false;
            }
        }

        return true;
    }

    private static string Render(
        IReadOnlyList<VerifiedClaimResult> supported,
        VerifiedAnswerStatus status)
    {
        var text = new StringBuilder();
        if (status == VerifiedAnswerStatus.Partial)
        {
            text.AppendLine("Réponse partielle : seuls les éléments vérifiés sont présentés.");
        }

        foreach (var claim in supported)
        {
            text.Append("- ").AppendLine(claim.Statement);
            foreach (var evidence in claim.Evidence)
            {
                text.Append("  Source ")
                    .Append(evidence.DocumentId.ToString("D"))
                    .Append(" / ")
                    .Append(evidence.ChunkId.ToString("D"))
                    .Append(" [")
                    .Append(evidence.StartUtf16)
                    .Append(':')
                    .Append(evidence.LengthUtf16)
                    .Append("] : «")
                    .Append(evidence.Quote)
                    .AppendLine("»");
            }
        }

        return text.ToString().TrimEnd();
    }
}
