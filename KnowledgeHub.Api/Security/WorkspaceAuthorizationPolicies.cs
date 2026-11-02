using KnowledgeHub.Domain.Entities;
using Microsoft.AspNetCore.Authorization;

namespace KnowledgeHub.Api.Security;

public enum WorkspaceCapability
{
    Read,
    AskRag,
    EditContent,
    ManageHome,
    ManageBranding,
    ManageMembers,
    Archive,
    Restore,
    TransferOwnership
}

/// <summary>
/// Policies are named explicitly so controllers cannot accidentally infer
/// authorization from a client's role claim.
/// </summary>
public static class WorkspaceAuthorizationPolicies
{
    public const string Authenticated = "KH.Workspace.Authenticated";
    public const string Read = "KH.Workspace.Read";
    public const string AskRag = "KH.Workspace.AskRag";
    public const string EditContent = "KH.Workspace.EditContent";
    public const string ManageHome = "KH.Workspace.ManageHome";
    public const string ManageBranding = "KH.Workspace.ManageBranding";
    public const string ManageMembers = "KH.Workspace.ManageMembers";
    public const string Archive = "KH.Workspace.Archive";
    public const string Restore = "KH.Workspace.Restore";
    public const string TransferOwnership = "KH.Workspace.TransferOwnership";

    public static void AddPolicies(AuthorizationOptions options)
    {
        options.AddPolicy(Authenticated, policy => policy.RequireAuthenticatedUser());
        Add(options, Read, WorkspaceCapability.Read);
        Add(options, AskRag, WorkspaceCapability.AskRag);
        Add(options, EditContent, WorkspaceCapability.EditContent);
        Add(options, ManageHome, WorkspaceCapability.ManageHome);
        Add(options, ManageBranding, WorkspaceCapability.ManageBranding);
        Add(options, ManageMembers, WorkspaceCapability.ManageMembers);
        Add(options, Archive, WorkspaceCapability.Archive);
        Add(options, Restore, WorkspaceCapability.Restore);
        Add(options, TransferOwnership, WorkspaceCapability.TransferOwnership);
    }

    private static void Add(AuthorizationOptions options, string name, WorkspaceCapability capability) =>
        options.AddPolicy(name, policy => policy.RequireAuthenticatedUser()
            .AddRequirements(new WorkspaceAccessRequirement(capability)));

    public static bool Allows(WorkspaceRole role, WorkspaceCapability capability) => role switch
    {
        WorkspaceRole.OWNER => true,
        WorkspaceRole.ADMIN => capability is WorkspaceCapability.Read or
            WorkspaceCapability.AskRag or WorkspaceCapability.EditContent or
            WorkspaceCapability.ManageHome or WorkspaceCapability.ManageBranding or
            WorkspaceCapability.ManageMembers,
        WorkspaceRole.EDITOR => capability is WorkspaceCapability.Read or
            WorkspaceCapability.AskRag or WorkspaceCapability.EditContent or
            WorkspaceCapability.ManageHome,
        WorkspaceRole.MEMBER => capability is WorkspaceCapability.Read or WorkspaceCapability.AskRag,
        WorkspaceRole.VIEWER => capability == WorkspaceCapability.Read,
        _ => false
    };
}

public sealed record WorkspaceAccessRequirement(WorkspaceCapability Capability) : IAuthorizationRequirement;
