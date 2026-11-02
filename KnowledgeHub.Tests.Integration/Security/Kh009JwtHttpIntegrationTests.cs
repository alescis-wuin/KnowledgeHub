using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using KnowledgeHub.Api.Security;
using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace KnowledgeHub.Tests.Integration.Security;

/// <summary>
/// Real RS256 Bearer access tokens on the HTTP pipeline. The signing public key
/// is injected into the *test host only*. No production bypass endpoint exists.
/// </summary>
public sealed class Kh009JwtHttpIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Issuer = "https://idp.kh009.example.test";
    private const string Audience = "knowledgehub.api";
    private const string ApiScope = "knowledgehub.read";
    private const string Subject = "alice-test-user";
    private static readonly Guid WorkspaceId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");
    private readonly CustomWebApplicationFactory _databaseFactory;

    public Kh009JwtHttpIntegrationTests(CustomWebApplicationFactory databaseFactory) =>
        _databaseFactory = databaseFactory;

    [Theory]
    [InlineData("absent")]
    [InlineData("tampered")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("scope")]
    [InlineData("idtoken")]
    [InlineData("untrusted-signature")]
    public async Task MissingOrInvalidTokenReturns401(string variant)
    {
        using var signingRsa = RSA.Create(2048);
        var signingKey = new RsaSecurityKey(signingRsa) { KeyId = "kh009-test-signature" };
        using var host = CreateHost(signingKey,
            new WorkspaceAccessSnapshot(WorkspaceRole.OWNER, WorkspaceMemberStatus.ACTIVE, false));
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var otherRsa = RSA.Create(2048);
        var otherKey = new RsaSecurityKey(otherRsa) { KeyId = "untrusted-test-key" };
        var token = CreateToken(variant == "untrusted-signature" ? otherKey : signingKey,
            issuer: variant == "issuer" ? "https://other.example.test" : Issuer,
            audience: variant == "audience" ? "other-api" : Audience,
            scope: variant == "scope" ? "wrong.scope" : ApiScope,
            typ: variant == "idtoken" ? "JWT" : "at+jwt",
            expired: variant == "expired", future: variant == "future");
        if (variant == "tampered")
        {
            var segments = token.Split('.');
            segments[1] = (segments[1][0] == 'a' ? 'b' : 'a') + segments[1][1..];
            token = string.Join('.', segments);
        }
        if (variant != "absent")
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.GetAsync($"/__kh009/security/{WorkspaceId:D}/read");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("missing", HttpStatusCode.NotFound)]
    [InlineData("viewer-read", HttpStatusCode.OK)]
    [InlineData("viewer-edit", HttpStatusCode.Forbidden)]
    [InlineData("editor-edit", HttpStatusCode.OK)]
    [InlineData("suspended", HttpStatusCode.NotFound)]
    [InlineData("archived", HttpStatusCode.NotFound)]
    public async Task ValidTokenUsesTrustedServerMembershipNotJwtRole(string scenario, HttpStatusCode expected)
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "kh009-test-signature" };
        var membership = scenario switch
        {
            "missing" => null,
            "viewer-read" or "viewer-edit" => new WorkspaceAccessSnapshot(
                WorkspaceRole.VIEWER, WorkspaceMemberStatus.ACTIVE, false),
            "editor-edit" => new WorkspaceAccessSnapshot(
                WorkspaceRole.EDITOR, WorkspaceMemberStatus.ACTIVE, false),
            "suspended" => new WorkspaceAccessSnapshot(
                WorkspaceRole.OWNER, WorkspaceMemberStatus.SUSPENDED, false),
            "archived" => new WorkspaceAccessSnapshot(
                WorkspaceRole.OWNER, WorkspaceMemberStatus.ACTIVE, true),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };
        var diagnostics = new ConcurrentQueue<string>();
        using var host = CreateHost(key, membership, diagnostics.Enqueue);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        // Catch registration-time configuration capture before making a request.
        var effectiveOptions = host.Services
            .GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.Equal(Issuer, effectiveOptions.TokenValidationParameters.ValidIssuer);
        Assert.Equal(Audience, effectiveOptions.TokenValidationParameters.ValidAudience);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(key));
        var path = scenario == "viewer-edit" || scenario == "editor-edit" ? "edit" : "read";
        using var response = await client.GetAsync($"/__kh009/security/{WorkspaceId:D}/{path}");
        Assert.True(response.StatusCode == expected,
            $"{scenario}: expected {expected}, received {response.StatusCode}; " +
            $"JWT stage: {string.Join("; ", diagnostics)}");
        if (expected is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync();
            Assert.DoesNotContain(Subject, body, StringComparison.Ordinal);
            Assert.DoesNotContain("OWNER", body, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task LegacyEndpointRemainsAvailableWithoutBearerToken()
    {
        using var rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "kh009-test-signature" };
        using var host = CreateHost(key, null);
        using var client = host.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        using var response = await client.GetAsync("/api/diagnostics/database");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private WebApplicationFactory<global::Program> CreateHost(
        SecurityKey trustedKey, WorkspaceAccessSnapshot? membership,
        Action<string>? diagnostics = null) =>
        _databaseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Authentication:Jwt:Authority"] = Issuer,
                    ["Authentication:Jwt:Issuer"] = Issuer,
                    ["Authentication:Jwt:Audience"] = Audience,
                    ["Authentication:Jwt:RequiredScope"] = ApiScope,
                    ["Authentication:TrustedIssuers:0"] = Issuer
                });
            });
            builder.ConfigureTestServices(services =>
            {
                services.AddControllers().AddApplicationPart(typeof(Kh009SecurityProbeController).Assembly);
                services.RemoveAll<IWorkspaceAccessStore>();
                services.AddScoped<IWorkspaceAccessStore>(_ => new TrustedTestStore(membership));
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    var metadata = new OpenIdConnectConfiguration { Issuer = Issuer };
                    metadata.SigningKeys.Add(trustedKey);
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(metadata);
                    if (diagnostics is null)
                        return;

                    // The test reports exception TYPES only, never JWTs or keys.
                    var priorAuthenticationFailed = options.Events.OnAuthenticationFailed;
                    options.Events.OnAuthenticationFailed = async context =>
                    {
                        diagnostics("authentication-failed:" + context.Exception.GetType().Name);
                        await priorAuthenticationFailed(context);
                    };
                    var priorTokenValidated = options.Events.OnTokenValidated;
                    options.Events.OnTokenValidated = async context =>
                    {
                        await priorTokenValidated(context);
                        diagnostics("token-validated:" +
                            (context.Principal?.Claims.Any(c => c.Type == "iss") == true
                                ? "issuer-claim-present" : "issuer-claim-missing") + ":" +
                            (context.Principal?.Claims.Any(c => c.Type == "sub") == true
                                ? "subject-claim-present" : "subject-claim-missing") + ":" +
                            (context.Result is null ? "not-rejected" : "rejected"));
                    };
                    var priorChallenge = options.Events.OnChallenge;
                    options.Events.OnChallenge = async context =>
                    {
                        diagnostics("challenge:" +
                            (context.AuthenticateFailure?.GetType().Name ?? "none"));
                        await priorChallenge(context);
                    };
                });
            });
        });

    private static string CreateToken(
        SecurityKey key, string issuer = Issuer, string audience = Audience,
        string scope = ApiScope, string typ = "at+jwt",
        bool expired = false, bool future = false)
    {
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims:
            [
                new Claim("sub", Subject),
                new Claim("scope", scope),
                // This untrusted claim must be ignored for workspace authorization.
                new Claim("role", "OWNER")
            ],
            notBefore: now.AddMinutes(future ? 5 : -15),
            expires: now.AddMinutes(expired ? -10 : 15),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));
        token.Header["typ"] = typ;
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class TrustedTestStore(WorkspaceAccessSnapshot? membership) : IWorkspaceAccessStore
    {
        public Task<WorkspaceAccessSnapshot?> FindAsync(
            OidcIdentityKey identity, Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(identity.Issuer == Issuer && identity.Subject == Subject &&
                workspaceId == WorkspaceId ? membership : null);
    }
}

/// <summary>Only loaded as an MVC ApplicationPart inside the integration test host.</summary>
[ApiController]
[Route("__kh009/security/{workspaceId:guid}")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class Kh009SecurityProbeController : ControllerBase
{
    [HttpGet("read")]
    [Authorize(Policy = WorkspaceAuthorizationPolicies.Read)]
    public IActionResult Read() => Ok(new { accepted = true });

    [HttpGet("edit")]
    [Authorize(Policy = WorkspaceAuthorizationPolicies.EditContent)]
    public IActionResult Edit() => Ok(new { accepted = true });
}
