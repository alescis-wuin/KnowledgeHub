using KnowledgeHub.Domain.Entities;
using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Application.Authorization;

/// <summary>
/// The only trusted source of workspace membership for authorization.
/// The production adapter is intentionally unavailable until KH-009.1.5.
/// Claims supplied by an identity provider never establish tenant membership.
/// </summary>
public interface IWorkspaceAccessStore
{
    Task<WorkspaceAccessSnapshot?> FindAsync(
        OidcIdentityKey identity,
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceAccessSnapshot(
    WorkspaceRole Role,
    WorkspaceMemberStatus Status,
    bool IsArchived);
