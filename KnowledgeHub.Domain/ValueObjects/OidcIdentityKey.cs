namespace KnowledgeHub.Domain.ValueObjects;

/// <summary>
/// Exact, case-sensitive OIDC identity. Neither email nor display name is a key.
/// An HTTP loopback issuer is permitted only for an explicitly trusted local IdP.
/// </summary>
public sealed record OidcIdentityKey
{
    public string Issuer { get; }
    public string Subject { get; }

    public OidcIdentityKey(string issuer, string subject)
    {
        if (string.IsNullOrWhiteSpace(issuer) || issuer.Length > 300 ||
            issuer != issuer.Trim() || issuer.Any(char.IsControl) ||
            !Uri.TryCreate(issuer, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            (uri.Scheme != Uri.UriSchemeHttps &&
             !(uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback)))
        {
            throw new ArgumentException("Invalid OIDC issuer identifier.", nameof(issuer));
        }

        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 300 ||
            subject != subject.Trim() || subject.Any(char.IsControl))
        {
            throw new ArgumentException("Invalid OIDC subject identifier.", nameof(subject));
        }

        // Preserve exact strings. No URL normalization or case folding: issuer
        // path, subject case, and trailing slash can change the identity.
        Issuer = issuer;
        Subject = subject;
    }
}
