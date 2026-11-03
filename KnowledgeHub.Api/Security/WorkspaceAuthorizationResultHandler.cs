using KnowledgeHub.Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace KnowledgeHub.Api.Security;

/// <summary>
/// Hide missing/non-member workspace identifiers with 404; preserve 401
/// Bearer challenge for absent/invalid tokens and 403 for insufficient roles.
/// </summary>
public sealed class WorkspaceAuthorizationResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _fallback = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext httpContext,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var workspacePolicy = policy.Requirements.Any(
            requirement => requirement is WorkspaceAccessRequirement);

        if (workspacePolicy && authorizeResult.Forbidden)
        {
            var isNotFound = Equals(
                httpContext.Items[WorkspaceAuthorizationHandler.FailureItemKey],
                WorkspaceAuthorizationHandler.NotFoundFailure);
            var status = isNotFound
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status403Forbidden;
            httpContext.Response.StatusCode = status;
            httpContext.Response.ContentType = "application/json; charset=utf-8";
            httpContext.Response.Headers["Cache-Control"] = "no-store";
            await httpContext.Response.WriteAsJsonAsync(
                ApiResponses.Failure<object>(
                    status,
                    httpContext.TraceIdentifier,
                    isNotFound ? "Ressource introuvable." : "Accès refusé."),
                cancellationToken: httpContext.RequestAborted);
            return;
        }

        await _fallback.HandleAsync(next, httpContext, policy, authorizeResult);
    }
}
