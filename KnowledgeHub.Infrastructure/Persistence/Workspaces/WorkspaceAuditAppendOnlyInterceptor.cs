using KnowledgeHub.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace KnowledgeHub.Infrastructure.Persistence.Workspaces;

/// <summary>
/// Rejects tracked EF UPDATE/DELETE operations for audit events. Raw SQL and
/// ExecuteUpdate/ExecuteDelete bypass this barrier; SQL role/trigger hardening
/// is deferred to KH-009.5 and must not be claimed here.
/// </summary>
public sealed class WorkspaceAuditAppendOnlyInterceptor : SaveChangesInterceptor
{
    public static WorkspaceAuditAppendOnlyInterceptor Instance { get; } = new();

    private WorkspaceAuditAppendOnlyInterceptor() { }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        RejectTrackedMutation(eventData.Context);
        return result;
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        RejectTrackedMutation(eventData.Context);
        return ValueTask.FromResult(result);
    }

    private static void RejectTrackedMutation(DbContext? context)
    {
        if (context is null)
            return;
        foreach (var entry in context.ChangeTracker.Entries())
        {
            if (entry.Entity is WorkspaceAuditEvent &&
                entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("Workspace audit events are append-only in tracked EF writes.");
        }
    }
}
