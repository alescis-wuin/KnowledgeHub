using System.Security.Claims;
using KnowledgeHub.Application.Identity;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Identity;

public sealed class Kh009OidcIdentityTests
{
    private const string IssuerA = "https://idp.example.test/realm/a";
    private const string IssuerB = "https://idp.example.test/realm/b";

    private static ClaimsPrincipal Principal(
        string issuer = IssuerA,
        string subject = "user-42",
        bool authenticated = true,
        params Claim[] extra)
    {
        Claim[] claims =
        [
            new Claim("iss", issuer),
            new Claim("sub", subject),
            .. extra
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(
            claims, authenticated ? "UnitTestValidatedBearer" : null));
    }

    [Fact]
    public void KeyUsesIssuerAndSubjectTogetherAndPreservesExactCase()
    {
        var a = new OidcIdentityKey(IssuerA, "Alice");
        Assert.Equal(a, new OidcIdentityKey(IssuerA, "Alice"));
        Assert.NotEqual(a, new OidcIdentityKey(IssuerA, "alice"));
        Assert.NotEqual(a, new OidcIdentityKey(IssuerB, "Alice"));
        Assert.NotEqual(a, new OidcIdentityKey(IssuerA + "/", "Alice"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("example.com")]
    [InlineData("http://idp.example.test")]
    [InlineData("https://idp.example.test/?q=1")]
    [InlineData("https://idp.example.test/#x")]
    [InlineData("https://user:pass@idp.example.test")]
    [InlineData(" https://idp.example.test")]
    public void RejectsInvalidIssuer(string issuer) =>
        Assert.Throws<ArgumentException>(() => new OidcIdentityKey(issuer, "person"));

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" leading")]
    [InlineData("trailing ")]
    [InlineData("line\nbreak")]
    public void RejectsInvalidSubject(string subject) =>
        Assert.Throws<ArgumentException>(() => new OidcIdentityKey(IssuerA, subject));

    [Fact]
    public void RejectsOverlongIssuerAndSubject()
    {
        Assert.Throws<ArgumentException>(() =>
            new OidcIdentityKey("https://example.test/" + new string('a', 310), "person"));
        Assert.Throws<ArgumentException>(() =>
            new OidcIdentityKey(IssuerA, new string('x', 301)));
    }

    [Fact]
    public void AllowsExplicitlyWhitelistedLoopbackIssuerOnly()
    {
        const string local = "http://localhost:8080/realms/dev";
        var key = new OidcIdentityKey(local, "user");
        Assert.Equal(local, key.Issuer);
        Assert.Null(new OidcPrincipalIdentityResolver([IssuerA]).Resolve(Principal(local)));
        Assert.Equal(local,
            new OidcPrincipalIdentityResolver([local]).Resolve(Principal(local))?.Issuer);
    }

    [Fact]
    public void AppUserKeepsStableIdentityWhenProfileChanges()
    {
        var key = new OidcIdentityKey(IssuerA, "person");
        var user = new AppUser(key, "  Alice  ");
        var id = user.Id;
        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal("Alice", user.DisplayName);
        Assert.Equal(DateTimeKind.Utc, user.CreatedAtUtc.Kind);
        user.SetProfile("Alice B", "  Alice@Example.test  ", true);
        Assert.Equal(id, user.Id);
        Assert.Equal(key.Issuer, user.Issuer);
        Assert.Equal(key.Subject, user.Subject);
        Assert.Equal("ALICE@EXAMPLE.TEST", user.EmailNormalized);
        Assert.True(user.EmailVerified);
    }

    [Fact]
    public void EmailIsOptionalMetadataButCannotBeVerifiedWhenAbsent()
    {
        var user = new AppUser(new OidcIdentityKey(IssuerA, "person"), "User");
        Assert.Null(user.EmailNormalized);
        Assert.False(user.EmailVerified);
        Assert.Throws<ArgumentException>(() => user.SetProfile("User", null, true));
        Assert.Null(user.EmailNormalized);
    }

    [Fact]
    public void ResolverAcceptsOnlyAuthenticatedPrincipalWithTrustedIssuer()
    {
        var resolver = new OidcPrincipalIdentityResolver([IssuerA]);
        Assert.Equal(new OidcIdentityKey(IssuerA, "user-42"), resolver.Resolve(Principal()));
        Assert.Null(resolver.Resolve(Principal(authenticated: false)));
        Assert.Null(resolver.Resolve(Principal(issuer: IssuerB)));
        Assert.Null(resolver.Resolve(null));
    }

    [Fact]
    public void ResolverHasNoImplicitTrustWhenConfiguredWithoutIssuer()
    {
        var resolver = new OidcPrincipalIdentityResolver([]);
        Assert.Null(resolver.Resolve(Principal()));
        Assert.Throws<ArgumentException>(() => new OidcPrincipalIdentityResolver(["http://remote.example.test"]));
    }

    [Fact]
    public void ResolverRejectsMissingAndDuplicateIdentityClaims()
    {
        var resolver = new OidcPrincipalIdentityResolver([IssuerA]);
        ClaimsPrincipal Missing(string claim) =>
            new(new ClaimsIdentity(new[] { new Claim(claim, "user-42") }, "test"));
        Assert.Null(resolver.Resolve(Missing("sub")));
        Assert.Null(resolver.Resolve(Missing("iss")));
        Assert.Null(resolver.Resolve(Principal(extra: [new Claim("iss", IssuerA)])));
        Assert.Null(resolver.Resolve(Principal(extra: [new Claim("sub", "other")])));
    }

    [Fact]
    public void ResolverRejectsMultipleIdentitiesInsteadOfMergingClaims()
    {
        var resolver = new OidcPrincipalIdentityResolver([IssuerA]);
        var principal = Principal();
        principal.AddIdentity(new ClaimsIdentity([new Claim("sub", "other")], "test"));
        Assert.Null(resolver.Resolve(principal));
    }

    [Fact]
    public void ResolverNeverUsesEmailNameOrClientRoleAsIdentity()
    {
        var resolver = new OidcPrincipalIdentityResolver([IssuerA]);
        var principal = Principal(extra:
        [
            new Claim("email", "spoof@example.test"),
            new Claim(ClaimTypes.NameIdentifier, "forged-name-id"),
            new Claim(ClaimTypes.Role, "OWNER")
        ]);
        Assert.Equal(new OidcIdentityKey(IssuerA, "user-42"), resolver.Resolve(principal));
    }
}
