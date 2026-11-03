namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Immutable section value within a WorkspaceHomepage aggregate. Links are
/// typed Domain values; JSON serialization belongs to the later EF mapping.
/// </summary>
public sealed class WorkspaceHomeSection
{
    private IReadOnlyList<WorkspaceHomeLink> _links = Array.Empty<WorkspaceHomeLink>();

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public WorkspaceSectionType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public int SortOrder { get; private set; }
    public IReadOnlyList<WorkspaceHomeLink> Links => _links;

    private WorkspaceHomeSection() { } // EF Core

    public WorkspaceHomeSection(
        Guid workspaceId,
        WorkspaceSectionType type,
        string title,
        string? body,
        int sortOrder,
        IEnumerable<WorkspaceHomeLink>? links = null)
    {
        WorkspacePresentationValidation.RequireId(workspaceId, nameof(workspaceId));
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));
        RequireOrder(sortOrder);
        var checkedTitle = WorkspacePresentationValidation.RequiredText(title, 150, nameof(title));
        var checkedLinks = (links ?? Array.Empty<WorkspaceHomeLink>()).Take(11).ToArray();
        if (checkedLinks.Any(link => link is null))
            throw new ArgumentException("Null links are not allowed.", nameof(links));

        string? checkedBody;
        switch (type)
        {
            case WorkspaceSectionType.TEXT:
                checkedBody = WorkspacePresentationValidation.RequiredText(body, 4000, nameof(body));
                if (checkedLinks.Length != 0)
                    throw new ArgumentException("TEXT sections cannot contain links.", nameof(links));
                break;
            case WorkspaceSectionType.LINKS:
                if (body is not null || checkedLinks.Length is < 1 or > 10)
                    throw new ArgumentException("LINKS requires 1-10 links and no body.", nameof(links));
                checkedBody = null;
                break;
            case WorkspaceSectionType.STATS:
                if (body is not null || checkedLinks.Length != 0)
                    throw new ArgumentException("STATS cannot contain arbitrary body or links.", nameof(body));
                checkedBody = null;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type));
        }

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Type = type;
        Title = checkedTitle;
        Body = checkedBody;
        SortOrder = sortOrder;
        _links = Array.AsReadOnly(checkedLinks);
    }

    private WorkspaceHomeSection(WorkspaceHomeSection original, int sortOrder)
    {
        RequireOrder(sortOrder);
        Id = original.Id;
        WorkspaceId = original.WorkspaceId;
        Type = original.Type;
        Title = original.Title;
        Body = original.Body;
        SortOrder = sortOrder;
        _links = original._links;
    }

    internal WorkspaceHomeSection WithSortOrder(int sortOrder) => new(this, sortOrder);

    private static void RequireOrder(int value)
    {
        if (value is < 0 or > 19)
            throw new ArgumentOutOfRangeException(nameof(value), "Order must be between 0 and 19.");
    }
}
