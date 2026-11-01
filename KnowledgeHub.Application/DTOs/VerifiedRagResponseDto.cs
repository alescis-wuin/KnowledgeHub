using KnowledgeHub.Application.Verification;

namespace KnowledgeHub.Application.DTOs;

/// <summary>
/// The deliberately narrow public contract for the verified endpoint.
/// No rejected claim, raw model result, question or assessment is serialized.
/// </summary>
public sealed record VerifiedRagResponseDto(
    string Verdict,
    string Answer,
    IReadOnlyList<VerifiedRagClaimDto> Claims)
{
    public const string SafeAbstention =
        "Preuves insuffisantes ou validation non disponible : aucune réponse factuelle publiée.";

    public static VerifiedRagResponseDto Abstain() =>
        new("ABSTAIN", SafeAbstention, []);

    public static VerifiedRagResponseDto FromVerifiedDecision(VerifiedEvidenceAnswer decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Status == VerifiedAnswerStatus.Conflict)
        {
            return new VerifiedRagResponseDto(
                "CONFLICT",
                "Conflit documentaire non résolu. Aucune conclusion factuelle publiée.",
                []);
        }

        if (decision.Status is not (VerifiedAnswerStatus.Answer or VerifiedAnswerStatus.Partial))
        {
            return Abstain();
        }

        // Never forward VerifiedEvidenceAnswer.Claims directly: it contains
        // untrusted rejected statements for diagnostics and policy reasoning.
        var published = decision.Claims
            .Where(claim => claim.Status == VerifiedClaimStatus.Supported)
            .Select(claim => new VerifiedRagClaimDto(
                claim.Id,
                claim.Statement,
                claim.Evidence.Select(pointer => new VerifiedRagCitationDto(
                    pointer.DocumentId,
                    pointer.ChunkId,
                    pointer.StartUtf16,
                    pointer.LengthUtf16,
                    pointer.Quote,
                    pointer.ChunkSha256)).ToArray()))
            .ToArray();

        if (published.Length == 0)
        {
            return Abstain();
        }

        return new VerifiedRagResponseDto(
            decision.Status == VerifiedAnswerStatus.Answer ? "ANSWER" : "PARTIAL",
            decision.Text,
            published);
    }
}

public sealed record VerifiedRagClaimDto(
    string Id,
    string Statement,
    IReadOnlyList<VerifiedRagCitationDto> Evidence);

public sealed record VerifiedRagCitationDto(
    Guid DocumentId,
    Guid ChunkId,
    int StartUtf16,
    int LengthUtf16,
    string Quote,
    string ChunkSha256);
