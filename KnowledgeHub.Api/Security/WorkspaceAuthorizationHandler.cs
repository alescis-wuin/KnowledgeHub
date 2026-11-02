using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Application.Identity;
using KnowledgeHub.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace KnowledgeHub.Api.Security;

/// <summary>
/// Resolves a workspace only from a server route value, then looks up
/// server-controlled membership using the validated (issuer, subject).
/// </summary>
public sealed class WorkspaceAuthorizationHandler : AuthorizationHandler<WorkspaceAccessRequirement>
{
    public const string FailureItemKey = "KnowledgeHub.KH009.WorkspaceAuthorizationFailure";
    public const string NotFoundFailure = "not-found";
    public const string InsufficientRoleFailure = "insufficient-role";

    private readonly IAuthenticatedUserIdentityResolver _identityResolver;
    private readonly IWorkspaceAccessStore _accessStore;

    public WorkspaceAuthorizationHandler(
        IAuthenticatedUserIdentityResolver identityResolver,
        IWorkspaceAccessStore accessStore)
    {
        _identityResolver = identityResolver;
        _accessStore = accessStore;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        WorkspaceAccessRequirement requirement)
    {
        if (context.Resource is not HttpContext httpContext)
            return;

        var rawId = httpContext.Request.RouteValues["workspaceId"]?.ToString();
        if (!Guid.TryParse(rawId, out var workspaceId) || workspaceId == Guid.Empty)
        {
            httpContext.Items[FailureItemKey] = NotFoundFailure;
            return;
        }

        var identity = _identityResolver.Resolve(context.User);
        if (identity is null)
        {
            httpContext.Items[FailureItemKey] = NotFoundFailure;
            return;
        }

        var grant = await _accessStore.FindAsync(
            identity, workspaceId, httpContext.RequestAborted);

        if (grant is null || grant.Status != WorkspaceMemberStatus.ACTIVE ||
            (grant.IsArchived && requirement.Capability != WorkspaceCapability.Restore))
        {
            httpContext.Items[FailureItemKey] = NotFoundFailure;
            return;
        }

        if (WorkspaceAuthorizationPolicies.Allows(grant.Role, requirement.Capability))
        {
            context.Succeed(requirement);
            return;
        }

        httpContext.Items[FailureItemKey] = InsufficientRoleFailure;
    }
}
