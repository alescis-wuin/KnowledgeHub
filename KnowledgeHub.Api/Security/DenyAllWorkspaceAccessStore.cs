using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Domain.ValueObjects;

namespace KnowledgeHub.Api.Security;

/// <summary>
/// Safe placeholder. Do not replace with a token-based role resolver.
/// A server-side EF Core implementation is required after the KH-009.1.5 migration.
/// </summary>
public sealed class DenyAllWorkspaceAccessStore : IWorkspaceAccessStore
{
    public Task<WorkspaceAccessSnapshot?> FindAsync(
        OidcIdentityKey identity,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult<WorkspaceAccessSnapshot?>(null);
    }
}
