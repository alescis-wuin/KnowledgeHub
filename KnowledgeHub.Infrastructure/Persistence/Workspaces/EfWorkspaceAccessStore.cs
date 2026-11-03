using KnowledgeHub.Application.Authorization;
using KnowledgeHub.Domain.ValueObjects;
using KnowledgeHub.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace KnowledgeHub.Infrastructure.Persistence.Workspaces;

/// <summary>
/// Fail-closed, server-side authorization lookup. The JWT's role/email claims
/// are never used to grant workspace access; only the OIDC (issuer, subject)
/// mapped to a persisted AppUser plus a matching membership are consulted.
/// The handler applies ACTIVE / archive / capability checks after this lookup.
/// </summary>
public sealed class EfWorkspaceAccessStore : IWorkspaceAccessStore
{
    private readonly KnowledgeDbContext _db;

    public EfWorkspaceAccessStore(KnowledgeDbContext db) =>
        _db = db ?? throw new ArgumentNullException(nameof(db));

    public async Task<WorkspaceAccessSnapshot?> FindAsync(
        OidcIdentityKey identity,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(identity);
        cancellationToken.ThrowIfCancellationRequested();
        if (workspaceId == Guid.Empty)
            return null;

        return await (
            from user in _db.AppUsers.AsNoTracking()
            join member in _db.WorkspaceMembers.AsNoTracking()
                on user.Id equals member.UserId
            join workspace in _db.Workspaces.AsNoTracking()
                on member.WorkspaceId equals workspace.Id
            where user.Issuer == identity.Issuer &&
                  user.Subject == identity.Subject &&
                  workspace.Id == workspaceId
            select new WorkspaceAccessSnapshot(
                member.Role,
                member.Status,
                workspace.ArchivedAtUtc != null)
        ).SingleOrDefaultAsync(cancellationToken);
    }
}
