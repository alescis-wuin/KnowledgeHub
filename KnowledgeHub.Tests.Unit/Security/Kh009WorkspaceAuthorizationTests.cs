using System.Security.Claims;
using KnowledgeHub.Api.Security;
using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Application.Identity;
using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace KnowledgeHub.Tests.Unit.Security;

public sealed class Kh009WorkspaceAuthorizationTests
{
    private const string Issuer = "https://identity.example.test/realm";
    private static readonly Guid Workspace = Guid.Parse("11111111-1111-4111-8111-111111111111");

    [Theory]
    [InlineData(WorkspaceRole.VIEWER, WorkspaceCapability.Read, true)]
    [InlineData(WorkspaceRole.VIEWER, WorkspaceCapability.AskRag, false)]
    [InlineData(WorkspaceRole.MEMBER, WorkspaceCapability.AskRag, true)]
    [InlineData(WorkspaceRole.MEMBER, WorkspaceCapability.EditContent, false)]
    [InlineData(WorkspaceRole.EDITOR, WorkspaceCapability.EditContent, true)]
    [InlineData(WorkspaceRole.EDITOR, WorkspaceCapability.ManageMembers, false)]
    [InlineData(WorkspaceRole.ADMIN, WorkspaceCapability.ManageMembers, true)]
    [InlineData(WorkspaceRole.ADMIN, WorkspaceCapability.Archive, false)]
    [InlineData(WorkspaceRole.OWNER, WorkspaceCapability.Archive, true)]
    [InlineData(WorkspaceRole.OWNER, WorkspaceCapability.TransferOwnership, true)]
    public void CapabilitiesAreIndependentOfTokenClaims(
        WorkspaceRole role, WorkspaceCapability capability, bool expected) =>
        Assert.Equal(expected, WorkspaceAuthorizationPolicies.Allows(role, capability));

    [Fact]
    public void MissingJwtConfigurationIsFailClosed()
    {
        var empty = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>()).Build();
        Assert.Null(Kh009JwtSettings.Read(empty));
        var incomplete = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Authentication:Jwt:Audience"] = "api" }).Build();
        Assert.Throws<InvalidOperationException>(() => Kh009JwtSettings.Read(incomplete));
    }

    [Fact]
    public void ValidConfigurationMustMatchTrustedIssuerExactly()
    {
        var settings = Configuration();
        Assert.NotNull(Kh009JwtSettings.Read(settings));
        var wrong = Configuration("https://identity.example.test/realm/");
        Assert.Throws<InvalidOperationException>(() => Kh009JwtSettings.Read(wrong));
    }

    [Theory]
    [InlineData("", false)]
    [InlineData("not-a-guid", false)]
    [InlineData("11111111-1111-4111-8111-111111111111", true)]
    public async Task WorkspaceIdMustComeFromRoute(string routeValue, bool expected)
    {
        var result = await EvaluateAsync(
            new WorkspaceAccessSnapshot(WorkspaceRole.OWNER, WorkspaceMemberStatus.ACTIVE, false),
            WorkspaceCapability.Read, routeValue);
        Assert.Equal(expected, result.Allowed);
    }

    [Fact]
    public async Task NonMemberCannotAccessEvenWithClaimedOwnerRole()
    {
        var result = await EvaluateAsync(null, WorkspaceCapability.Read, Workspace.ToString("D"));
        Assert.False(result.Allowed);
        Assert.Equal(WorkspaceAuthorizationHandler.NotFoundFailure, result.Failure);
    }

    [Theory]
    [InlineData(WorkspaceMemberStatus.INVITED)]
    [InlineData(WorkspaceMemberStatus.SUSPENDED)]
    public async Task InactiveMemberDoesNotGainAccess(WorkspaceMemberStatus status)
    {
        var result = await EvaluateAsync(
            new WorkspaceAccessSnapshot(WorkspaceRole.OWNER, status, false),
            WorkspaceCapability.Read, Workspace.ToString("D"));
        Assert.False(result.Allowed);
        Assert.Equal(WorkspaceAuthorizationHandler.NotFoundFailure, result.Failure);
    }

    [Fact]
    public async Task InsufficientRoleIsDifferentFromNonMember()
    {
        var result = await EvaluateAsync(
            new WorkspaceAccessSnapshot(WorkspaceRole.VIEWER, WorkspaceMemberStatus.ACTIVE, false),
            WorkspaceCapability.EditContent, Workspace.ToString("D"));
        Assert.False(result.Allowed);
        Assert.Equal(WorkspaceAuthorizationHandler.InsufficientRoleFailure, result.Failure);
    }

    [Fact]
    public async Task ArchivedWorkspaceCanOnlyBeRestoredByOwner()
    {
        var grant = new WorkspaceAccessSnapshot(WorkspaceRole.OWNER, WorkspaceMemberStatus.ACTIVE, true);
        Assert.False((await EvaluateAsync(grant, WorkspaceCapability.Read, Workspace.ToString("D"))).Allowed);
        Assert.True((await EvaluateAsync(grant, WorkspaceCapability.Restore, Workspace.ToString("D"))).Allowed);
    }

    [Fact]
    public async Task AbsenceOfAuthenticatedPrincipalIsDenied()
    {
        var result = await EvaluateAsync(
            new WorkspaceAccessSnapshot(WorkspaceRole.OWNER, WorkspaceMemberStatus.ACTIVE, false),
            WorkspaceCapability.Read, Workspace.ToString("D"), authenticated: false);
        Assert.False(result.Allowed);
    }

    private static IConfiguration Configuration(string? trustedIssuer = null) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Authentication:Jwt:Authority"] = Issuer,
            ["Authentication:Jwt:Issuer"] = Issuer,
            ["Authentication:Jwt:Audience"] = "knowledgehub.api",
            ["Authentication:Jwt:RequiredScope"] = "knowledgehub.read",
            ["Authentication:TrustedIssuers:0"] = trustedIssuer ?? Issuer
        }).Build();

    private static async Task<(bool Allowed, string? Failure)> EvaluateAsync(
        WorkspaceAccessSnapshot? grant, WorkspaceCapability capability, string routeValue,
        bool authenticated = true)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("iss", Issuer), new Claim("sub", "subject"),
             new Claim("role", "OWNER")], authenticated ? "Bearer" : null));
        var http = new DefaultHttpContext();
        http.Request.RouteValues["workspaceId"] = routeValue;
        var requirement = new WorkspaceAccessRequirement(capability);
        var context = new AuthorizationHandlerContext([requirement], principal, http);
        var handler = new WorkspaceAuthorizationHandler(
            new OidcPrincipalIdentityResolver([Issuer]), new FakeStore(grant));
        await handler.HandleAsync(context);
        return (context.HasSucceeded,
            http.Items[WorkspaceAuthorizationHandler.FailureItemKey] as string);
    }

    private sealed class FakeStore(WorkspaceAccessSnapshot? grant) : IWorkspaceAccessStore
    {
        public Task<WorkspaceAccessSnapshot?> FindAsync(
            OidcIdentityKey identity, Guid workspaceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(workspaceId == Workspace ? grant : null);
    }
}
