namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Append-only structured audit metadata. Database UPDATE/DELETE protection
/// and retention belong to later authorized infrastructure milestones.
/// </summary>
public sealed class WorkspaceAuditEvent
{
    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public Guid? ActorUserId { get; private set; }
    public WorkspaceAuditEventType EventType { get; private set; }
    public WorkspaceAuditTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public WorkspaceAuditOutcome Outcome { get; private set; }
    public DateTime OccurredAtUtc { get; private set; }

    private WorkspaceAuditEvent() { } // EF Core

    /// <summary>Actor must be obtained from trusted server identity, never a request DTO.</summary>
    public static WorkspaceAuditEvent ForActor(
        Guid workspaceId,
        Guid actorUserId,
        WorkspaceAuditEventType eventType,
        WorkspaceAuditTargetType targetType,
        Guid targetId,
        WorkspaceAuditOutcome outcome,
        DateTime occurredAtUtc)
    {
        WorkspacePresentationValidation.RequireId(actorUserId, nameof(actorUserId));
        return Create(workspaceId, actorUserId, eventType, targetType, targetId, outcome, occurredAtUtc);
    }

    /// <summary>Only a server-authorized system service may use this factory.</summary>
    public static WorkspaceAuditEvent ForSystem(
        Guid workspaceId,
        WorkspaceAuditEventType eventType,
        WorkspaceAuditTargetType targetType,
        Guid targetId,
        WorkspaceAuditOutcome outcome,
        DateTime occurredAtUtc) =>
        Create(workspaceId, null, eventType, targetType, targetId, outcome, occurredAtUtc);

    private static WorkspaceAuditEvent Create(
        Guid workspaceId,
        Guid? actorUserId,
        WorkspaceAuditEventType eventType,
        WorkspaceAuditTargetType targetType,
        Guid targetId,
        WorkspaceAuditOutcome outcome,
        DateTime occurredAtUtc)
    {
        WorkspacePresentationValidation.RequireId(workspaceId, nameof(workspaceId));
        WorkspacePresentationValidation.RequireId(targetId, nameof(targetId));
        Workspace.RequireUtc(occurredAtUtc, nameof(occurredAtUtc));
        if (!Enum.IsDefined(eventType) || !Enum.IsDefined(targetType) || !Enum.IsDefined(outcome))
            throw new ArgumentOutOfRangeException(nameof(eventType), "Unknown audit enum value.");

        var expectedTarget = eventType switch
        {
            WorkspaceAuditEventType.WORKSPACE_CREATED or
            WorkspaceAuditEventType.WORKSPACE_UPDATED or
            WorkspaceAuditEventType.WORKSPACE_ARCHIVED or
            WorkspaceAuditEventType.WORKSPACE_RESTORED or
            WorkspaceAuditEventType.ORGANIZATION_VERIFICATION_CHANGED => WorkspaceAuditTargetType.WORKSPACE,
            WorkspaceAuditEventType.OWNER_TRANSFERRED or
            WorkspaceAuditEventType.MEMBER_ROLE_CHANGED or
            WorkspaceAuditEventType.MEMBER_SUSPENDED or
            WorkspaceAuditEventType.MEMBER_REACTIVATED => WorkspaceAuditTargetType.MEMBER,
            WorkspaceAuditEventType.INVITATION_CREATED or
            WorkspaceAuditEventType.INVITATION_ACCEPTED or
            WorkspaceAuditEventType.INVITATION_REVOKED => WorkspaceAuditTargetType.INVITATION,
            WorkspaceAuditEventType.BRANDING_UPDATED => WorkspaceAuditTargetType.BRANDING,
            WorkspaceAuditEventType.HOMEPAGE_UPDATED => WorkspaceAuditTargetType.HOMEPAGE,
            WorkspaceAuditEventType.ASSET_CREATED or
            WorkspaceAuditEventType.ASSET_DELETED => WorkspaceAuditTargetType.ASSET,
            _ => throw new ArgumentOutOfRangeException(nameof(eventType))
        };
        if (targetType != expectedTarget ||
            ((targetType is WorkspaceAuditTargetType.WORKSPACE or
                WorkspaceAuditTargetType.BRANDING or WorkspaceAuditTargetType.HOMEPAGE) &&
             targetId != workspaceId))
            throw new ArgumentException("Audit target does not match event or workspace.", nameof(targetType));

        return new WorkspaceAuditEvent
        {
            Id = Guid.NewGuid(),
            WorkspaceId = workspaceId,
            ActorUserId = actorUserId,
            EventType = eventType,
            TargetType = targetType,
            TargetId = targetId,
            Outcome = outcome,
            OccurredAtUtc = occurredAtUtc
        };
    }
}
