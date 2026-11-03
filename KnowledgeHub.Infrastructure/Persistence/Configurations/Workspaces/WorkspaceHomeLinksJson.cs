using System.Text.Json;
using KnowledgeHub.Domain.Entities;

namespace KnowledgeHub.Infrastructure.Persistence.Configurations.Workspaces;

/// <summary>
/// Persist strictly typed internal links in a jsonb column. This is not an
/// arbitrary HTML/URL payload. The domain validates labels and enum targets.
/// </summary>
internal static class WorkspaceHomeLinksJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true
    };

    internal static string Serialize(IReadOnlyList<WorkspaceHomeLink> links) =>
        JsonSerializer.Serialize(links, Options);

    internal static IReadOnlyList<WorkspaceHomeLink> Deserialize(string json)
    {
        var links = JsonSerializer.Deserialize<WorkspaceHomeLink[]>(json, Options)
            ?? throw new JsonException("Link JSON must be an array.");
        if (links.Length > 10 || links.Any(link => link is null))
            throw new JsonException("Invalid links array.");
        return Array.AsReadOnly(links);
    }

    internal static bool Equivalent(
        IReadOnlyList<WorkspaceHomeLink>? left,
        IReadOnlyList<WorkspaceHomeLink>? right)
    {
        if (ReferenceEquals(left, right))
            return true;
        if (left is null || right is null || left.Count != right.Count)
            return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i].Target != right[i].Target ||
                !string.Equals(left[i].Label, right[i].Label, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    internal static int Hash(IReadOnlyList<WorkspaceHomeLink> links)
    {
        var hash = new HashCode();
        foreach (var link in links)
        {
            hash.Add(link.Label, StringComparer.Ordinal);
            hash.Add(link.Target);
        }
        return hash.ToHashCode();
    }

    internal static IReadOnlyList<WorkspaceHomeLink> Snapshot(IReadOnlyList<WorkspaceHomeLink> links) =>
        Array.AsReadOnly(links.ToArray());
}
