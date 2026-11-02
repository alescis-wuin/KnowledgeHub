namespace KnowledgeHub.Domain.Entities;

/// <summary>Increasing capabilities. Special operations still need explicit authorization.</summary>
public enum WorkspaceRole
{
    VIEWER = 0,
    MEMBER = 1,
    EDITOR = 2,
    ADMIN = 3,
    OWNER = 4
}
