using System.Security.Cryptography;

namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Only a SHA-256 digest of a cryptographically random invitation token is
/// stored. Generation, delivery, rate limits and transactional redemption
/// belong to the authenticated application service (KH-009.2).
/// </summary>
public sealed class WorkspaceInvitation
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid InviterUserId { get; private set; }
    public string EmailNormalized { get; private set; } = string.Empty;
    public WorkspaceRole Role { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime ExpiresAtUtc { get; private set; }
    public DateTime? AcceptedAtUtc { get; private set; }
    public Guid? AcceptedByUserId { get; private set; }
    public DateTime? RevokedAtUtc { get; private set; }
    public long Version { get; private set; }

    private WorkspaceInvitation() { } // EF Core

    public WorkspaceInvitation(
        Guid workspaceId,
        Guid inviterUserId,
        string email,
        WorkspaceRole role,
        string tokenSha256,
        DateTime expiresAtUtc,
        DateTime createdAtUtc)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID required.", nameof(workspaceId));
        if (inviterUserId == Guid.Empty)
            throw new ArgumentException("Inviter ID required.", nameof(inviterUserId));
        if (!Enum.IsDefined(role) || role == WorkspaceRole.OWNER)
            throw new ArgumentOutOfRangeException(nameof(role), "Owner invitations are forbidden.");
        if (!IsValidSha256(tokenSha256))
            throw new ArgumentException("Expected a lowercase SHA-256 digest.", nameof(tokenSha256));

        Workspace.RequireUtc(expiresAtUtc, nameof(expiresAtUtc));
        Workspace.RequireUtc(createdAtUtc, nameof(createdAtUtc));
        if (expiresAtUtc <= createdAtUtc ||
            expiresAtUtc - createdAtUtc > TimeSpan.FromDays(14))
            throw new ArgumentOutOfRangeException(nameof(expiresAtUtc), "Invitation must expire within 14 days.");

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        InviterUserId = inviterUserId;
        EmailNormalized = NormalizeEmail(email);
        Role = role;
        TokenHash = tokenSha256;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Version = 1;
    }

    /// <summary>
    /// Caller must supply a SHA-256 hash of the presented one-time token,
    /// never a token from the request directly. The AppUser must be read from
    /// trusted persistence and derived from the authenticated OIDC principal.
    /// </summary>
    public void Accept(AppUser verifiedUser, string presentedTokenSha256, DateTime nowUtc)
    {
        ArgumentNullException.ThrowIfNull(verifiedUser);
        Workspace.RequireUtc(nowUtc, nameof(nowUtc));

        if (AcceptedAtUtc.HasValue || RevokedAtUtc.HasValue || nowUtc >= ExpiresAtUtc ||
            nowUtc < CreatedAtUtc || !verifiedUser.EmailVerified ||
            !string.Equals(verifiedUser.EmailNormalized, EmailNormalized, StringComparison.Ordinal) ||
            !IsValidSha256(presentedTokenSha256))
            throw new InvalidOperationException("Invitation cannot be accepted.");

        // Compare fixed-length decoded digests in constant time.
        if (!CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(TokenHash),
                Convert.FromHexString(presentedTokenSha256)))
            throw new InvalidOperationException("Invitation cannot be accepted.");

        if (Version == long.MaxValue)
            throw new InvalidOperationException("Invitation version overflow.");

        AcceptedAtUtc = nowUtc;
        AcceptedByUserId = verifiedUser.Id;
        Version++;
    }

    public void Revoke(DateTime nowUtc)
    {
        Workspace.RequireUtc(nowUtc, nameof(nowUtc));
        if (RevokedAtUtc.HasValue || AcceptedAtUtc.HasValue || nowUtc < CreatedAtUtc)
            throw new InvalidOperationException("Invitation cannot be revoked.");
        if (Version == long.MaxValue)
            throw new InvalidOperationException("Invitation version overflow.");

        RevokedAtUtc = nowUtc;
        Version++;
    }

    private static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email required.", nameof(email));
        var normalized = email.Trim().ToUpperInvariant();
        if (normalized.Length > 320 || normalized.Any(char.IsWhiteSpace) ||
            normalized.Any(char.IsControl) ||
            normalized.IndexOf('@') <= 0 ||
            normalized.EndsWith('@'))
            throw new ArgumentException("Invalid email.", nameof(email));
        return normalized;
    }

    private static bool IsValidSha256(string? value) =>
        value is { Length: 64 } &&
        value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');
}
