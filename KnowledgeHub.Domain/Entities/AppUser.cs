using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Provider-neutral application user. Persistence and unique (Issuer, Subject)
/// constraint are intentionally deferred to KH-009.1.3 / KH-009.1.5.
/// </summary>
public sealed class AppUser
{
    public Guid Id { get; private set; }
    public string Issuer { get; private set; } = string.Empty;
    public string Subject { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? EmailNormalized { get; private set; }
    public bool EmailVerified { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    private AppUser() { } // EF Core materialization in a later milestone.

    public AppUser(OidcIdentityKey identity, string displayName)
    {
        ArgumentNullException.ThrowIfNull(identity);
        Id = Guid.NewGuid();
        Issuer = identity.Issuer;
        Subject = identity.Subject;
        CreatedAtUtc = DateTime.UtcNow;
        SetProfile(displayName, null, false);
    }

    /// <summary>
    /// Called only from server-validated identity information, never from a
    /// caller-supplied workspace DTO. Email must never be used as a login key.
    /// </summary>
    public void SetProfile(string displayName, string? email, bool emailVerified)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 150 ||
            displayName.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid display name.", nameof(displayName));
        }

        var normalized = string.IsNullOrWhiteSpace(email)
            ? null
            : email.Trim().ToUpperInvariant();

        if (normalized is { Length: > 320 } ||
            (normalized is not null && normalized.Any(char.IsControl)))
        {
            throw new ArgumentException("Invalid email metadata.", nameof(email));
        }

        if (emailVerified && normalized is null)
        {
            throw new ArgumentException(
                "A verified email cannot be empty.", nameof(email));
        }

        DisplayName = displayName.Trim();
        EmailNormalized = normalized;
        EmailVerified = emailVerified;
    }
}
