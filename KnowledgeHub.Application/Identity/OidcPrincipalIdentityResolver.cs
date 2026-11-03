using System.Security.Claims;
using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Application.Identity;

/// <summary>
/// Fail-closed extraction of raw OIDC 'iss' and 'sub' claims. The JWT Bearer
/// handler (KH-009.1.4) MUST validate signature, issuer, audience, lifetime,
/// token type and access-token requirements with MapInboundClaims=false.
/// A constructed ClaimsPrincipal alone is NOT evidence of token validation.
/// </summary>
public sealed class OidcPrincipalIdentityResolver : IAuthenticatedUserIdentityResolver
{
    private readonly HashSet<string> _trustedIssuers;

    public OidcPrincipalIdentityResolver(IEnumerable<string> trustedIssuers)
    {
        ArgumentNullException.ThrowIfNull(trustedIssuers);
        _trustedIssuers = new HashSet<string>(StringComparer.Ordinal);
        foreach (var issuer in trustedIssuers)
        {
            // Invalid configuration fails immediately rather than being ignored.
            var checkedIssuer = new OidcIdentityKey(issuer, "configuration-check").Issuer;
            _trustedIssuers.Add(checkedIssuer);
        }
    }

    public OidcIdentityKey? Resolve(ClaimsPrincipal? principal)
    {
        if (principal is null)
        {
            return null;
        }

        // Do not mix claims between independently authenticated identities.
        var identities = principal.Identities.ToArray();
        if (identities.Length != 1 || !identities[0].IsAuthenticated)
        {
            return null;
        }

        var issuerClaims = identities[0].FindAll("iss").ToArray();
        var subjectClaims = identities[0].FindAll("sub").ToArray();
        if (issuerClaims.Length != 1 || subjectClaims.Length != 1 ||
            !_trustedIssuers.Contains(issuerClaims[0].Value))
        {
            return null;
        }

        try
        {
            return new OidcIdentityKey(issuerClaims[0].Value, subjectClaims[0].Value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
