using KnowledgeHub.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;

namespace KnowledgeHub.Api.Security;

/// <summary>
/// No provider is configured by default. Partial configurations fail startup
/// rather than silently accepting a weaker token-validation policy.
/// </summary>
public sealed record Kh009JwtSettings(
    string Authority,
    string Issuer,
    string Audience,
    string RequiredScope)
{
    public static Kh009JwtSettings? Read(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var authority = configuration["Authentication:Jwt:Authority"];
        var issuer = configuration["Authentication:Jwt:Issuer"];
        var audience = configuration["Authentication:Jwt:Audience"];
        var scope = configuration["Authentication:Jwt:RequiredScope"];
        var trusted = configuration.GetSection("Authentication:TrustedIssuers")
            .GetChildren().Select(child => child.Value).ToArray();

        if (new[] { authority, issuer, audience, scope }
            .All(string.IsNullOrWhiteSpace) && trusted.Length == 0)
            return null;

        if (string.IsNullOrWhiteSpace(authority) ||
            string.IsNullOrWhiteSpace(issuer) ||
            string.IsNullOrWhiteSpace(audience) ||
            string.IsNullOrWhiteSpace(scope))
            throw new InvalidOperationException("Incomplete KH009 OIDC JWT configuration.");

        if (authority != authority.Trim() ||
            !Uri.TryCreate(authority, UriKind.Absolute, out var url) ||
            url.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(url.UserInfo) ||
            !string.IsNullOrEmpty(url.Query) ||
            !string.IsNullOrEmpty(url.Fragment))
            throw new InvalidOperationException("OIDC discovery Authority must use HTTPS.");

        try { _ = new OidcIdentityKey(issuer, "configuration-subject"); }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Invalid trusted JWT issuer configuration.", ex);
        }

        if (trusted.Length != 1 ||
            !string.Equals(trusted[0], issuer, StringComparison.Ordinal))
            throw new InvalidOperationException("TrustedIssuers must contain exactly the JWT issuer.");

        if (audience.Length > 300 || audience != audience.Trim() ||
            audience.Any(char.IsWhiteSpace) ||
            scope.Length > 160 || scope.Any(char.IsWhiteSpace))
            throw new InvalidOperationException("Invalid JWT audience or required scope.");

        return new Kh009JwtSettings(authority, issuer, audience, scope);
    }
}
