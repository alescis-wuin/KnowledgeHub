namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Persistent membership; never infer its Role or Status from client-supplied claims.
/// Last-owner safety also requires a serialized/locked DB operation in KH-009.2.
/// </summary>
public sealed class WorkspaceMember
{
    public Guid WorkspaceId { get; private set; }
    public Guid UserId { get; private set; }
    public WorkspaceRole Role { get; private set; }
    public WorkspaceMemberStatus Status { get; private set; }
    public DateTime JoinedAtUtc { get; private set; }
    public long Version { get; private set; }

    private WorkspaceMember() { } // EF Core

    public WorkspaceMember(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTime? joinedAtUtc = null)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("Workspace ID required.", nameof(workspaceId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID required.", nameof(userId));
        RequireRole(role);

        var now = joinedAtUtc ?? DateTime.UtcNow;
        Workspace.RequireUtc(now, nameof(joinedAtUtc));

        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        Status = WorkspaceMemberStatus.ACTIVE;
        JoinedAtUtc = now;
        Version = 1;
    }

    /// <param name="activeOwnerCount">
    /// Must be counted under DB locks/serializable transaction by the service.
    /// The number alone is not authorization or concurrency protection.
    /// </param>
    public void ChangeRole(WorkspaceRole newRole, int activeOwnerCount)
    {
        RequireRole(newRole);
        RequireActive();
        RequireNotLastOwner(newRole != WorkspaceRole.OWNER, activeOwnerCount);

        if (Role == newRole)
            return;

        BumpVersion();
        Role = newRole;
    }

    public void Suspend()
    {
        RequireActive();
        // The relational model forbids a suspended OWNER. Demote under a
        // serialized transaction first, preserving at least one active owner.
        if (Role == WorkspaceRole.OWNER)
            throw new InvalidOperationException("Owners must transfer/demote before suspension.");
        Status = WorkspaceMemberStatus.SUSPENDED;
        BumpVersion();
    }

    public void Reactivate()
    {
        if (Status != WorkspaceMemberStatus.SUSPENDED || Role == WorkspaceRole.OWNER)
            throw new InvalidOperationException("Membership cannot be reactivated.");

        Status = WorkspaceMemberStatus.ACTIVE;
        BumpVersion();
    }

    private void RequireActive()
    {
        if (Status != WorkspaceMemberStatus.ACTIVE)
            throw new InvalidOperationException("Membership is not active.");
    }

    private void RequireNotLastOwner(bool removingOwnership, int activeOwnerCount)
    {
        if (Role == WorkspaceRole.OWNER && removingOwnership && activeOwnerCount <= 1)
            throw new InvalidOperationException("Cannot remove the last active owner.");
    }

    private void BumpVersion()
    {
        if (Version == long.MaxValue)
            throw new InvalidOperationException("Membership version overflow.");
        Version++;
    }

    private static void RequireRole(WorkspaceRole role)
    {
        if (!Enum.IsDefined(role))
            throw new ArgumentOutOfRangeException(nameof(role));
    }
}
