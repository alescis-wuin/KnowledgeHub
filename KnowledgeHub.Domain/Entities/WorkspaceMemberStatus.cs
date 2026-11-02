namespace KnowledgeHub.Domain.Entities;

/// <summary>INVITED is reserved; accepted invitations create ACTIVE membership.</summary>
public enum WorkspaceMemberStatus
{
    INVITED = 0,
    ACTIVE = 1,
    SUSPENDED = 2
}
