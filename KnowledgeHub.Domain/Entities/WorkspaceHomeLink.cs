namespace KnowledgeHub.Domain.Entities;

/// <summary>Safe, workspace-local navigation target; never an arbitrary URL.</summary>
public sealed class WorkspaceHomeLink
{
    public string Label { get; }
    public WorkspaceHomeLinkTarget Target { get; }

    public WorkspaceHomeLink(string label, WorkspaceHomeLinkTarget target)
    {
        if (!Enum.IsDefined(target))
            throw new ArgumentOutOfRangeException(nameof(target));
        Label = WorkspacePresentationValidation.RequiredText(label, 100, nameof(label));
        Target = target;
    }
}
