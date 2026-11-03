namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Versioned root of the homepage aggregate, including ordered sections.
/// Authorization and atomic DB replacement remain application responsibilities.
/// </summary>
public sealed class WorkspaceHomepage
{
    private readonly List<WorkspaceHomeSection> _sections = new();

    public Guid WorkspaceId { get; private set; }
    public string HeaderTitle { get; private set; } = string.Empty;
    public string? Subtitle { get; private set; }
    public string? WelcomeMessage { get; private set; }
    public long Version { get; private set; }
    public IReadOnlyList<WorkspaceHomeSection> Sections => _sections.AsReadOnly();

    private WorkspaceHomepage() { } // EF Core

    public WorkspaceHomepage(
        Guid workspaceId,
        string headerTitle,
        string? subtitle = null,
        string? welcomeMessage = null,
        IEnumerable<WorkspaceHomeSection>? sections = null)
    {
        WorkspacePresentationValidation.RequireId(workspaceId, nameof(workspaceId));
        var validatedTitle = WorkspacePresentationValidation.RequiredText(headerTitle, 150, nameof(headerTitle));
        var validatedSubtitle = WorkspacePresentationValidation.OptionalText(subtitle, 300, nameof(subtitle));
        var validatedWelcome = WorkspacePresentationValidation.OptionalText(welcomeMessage, 2000, nameof(welcomeMessage));
        var validatedSections = ValidateSections(workspaceId, sections ?? Array.Empty<WorkspaceHomeSection>());

        WorkspaceId = workspaceId;
        HeaderTitle = validatedTitle;
        Subtitle = validatedSubtitle;
        WelcomeMessage = validatedWelcome;
        _sections.AddRange(validatedSections);
        Version = 1;
    }

    public void Replace(
        string headerTitle,
        string? subtitle,
        string? welcomeMessage,
        IEnumerable<WorkspaceHomeSection> sections,
        long expectedVersion)
    {
        WorkspacePresentationValidation.RequireVersion(Version, expectedVersion);
        var validatedTitle = WorkspacePresentationValidation.RequiredText(headerTitle, 150, nameof(headerTitle));
        var validatedSubtitle = WorkspacePresentationValidation.OptionalText(subtitle, 300, nameof(subtitle));
        var validatedWelcome = WorkspacePresentationValidation.OptionalText(welcomeMessage, 2000, nameof(welcomeMessage));
        var validatedSections = ValidateSections(WorkspaceId, sections);

        HeaderTitle = validatedTitle;
        Subtitle = validatedSubtitle;
        WelcomeMessage = validatedWelcome;
        _sections.Clear();
        _sections.AddRange(validatedSections);
        Version++;
    }

    /// <summary>
    /// Reorder only currently owned section IDs. A future PUT service must
    /// separately authorize IDs of existing sections against persistence.
    /// </summary>
    public void Reorder(IReadOnlyList<Guid> orderedIds, long expectedVersion)
    {
        WorkspacePresentationValidation.RequireVersion(Version, expectedVersion);
        ArgumentNullException.ThrowIfNull(orderedIds);
        if (orderedIds.Count != _sections.Count ||
            orderedIds.Distinct().Count() != orderedIds.Count)
            throw new ArgumentException("Reordering must contain each current section exactly once.", nameof(orderedIds));

        var byId = _sections.ToDictionary(section => section.Id);
        var ordered = new List<WorkspaceHomeSection>(orderedIds.Count);
        for (var i = 0; i < orderedIds.Count; i++)
        {
            if (!byId.TryGetValue(orderedIds[i], out var section))
                throw new ArgumentException("Foreign section ID.", nameof(orderedIds));
            ordered.Add(section.WithSortOrder(i));
        }

        _sections.Clear();
        _sections.AddRange(ordered);
        Version++;
    }

    private static List<WorkspaceHomeSection> ValidateSections(
        Guid workspaceId, IEnumerable<WorkspaceHomeSection>? sections)
    {
        ArgumentNullException.ThrowIfNull(sections);
        var items = sections.Take(21).ToList();
        if (items.Count > 20 || items.Any(s => s is null || s.WorkspaceId != workspaceId) ||
            items.Select(s => s.Id).Distinct().Count() != items.Count ||
            items.Select(s => s.SortOrder).Distinct().Count() != items.Count)
            throw new ArgumentException("Invalid tenant, duplicate ID/order or too many sections.", nameof(sections));
        items.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        for (var i = 0; i < items.Count; i++)
        {
            if (items[i].SortOrder != i)
                throw new ArgumentException("Section order must be contiguous, starting at zero.", nameof(sections));
        }
        return items;
    }
}
