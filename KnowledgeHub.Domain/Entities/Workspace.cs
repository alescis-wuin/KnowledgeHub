using System.Text.RegularExpressions;

namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// An organizational boundary. Mutations are not proof of authorization:
/// services must check the authenticated actor and hold a DB transaction.
/// </summary>
public sealed class Workspace
{
    private static readonly Regex SlugPattern = new(
        "^[a-z0-9]+(?:-[a-z0-9]+)*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled,
        TimeSpan.FromMilliseconds(100));

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string SlugNormalized { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ArchivedAtUtc { get; private set; }
    public bool IsVerifiedOrganization { get; private set; }
    public long Version { get; private set; }

    private Workspace() { } // EF Core

    public Workspace(
        Guid createdByUserId,
        string name,
        string slug,
        string? description = null,
        DateTime? createdAtUtc = null)
    {
        if (createdByUserId == Guid.Empty)
            throw new ArgumentException("A creator is required.", nameof(createdByUserId));

        var now = createdAtUtc ?? DateTime.UtcNow;
        RequireUtc(now, nameof(createdAtUtc));

        Id = Guid.NewGuid();
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = now;
        SlugNormalized = ValidateSlug(slug);
        SetInformation(name, description);
        Version = 1;
    }

    public void UpdateInformation(string name, string? description, long expectedVersion)
    {
        RequireActive();
        RequireVersion(expectedVersion);
        SetInformation(name, description);
        Version++;
    }

    public void Archive(long expectedVersion, DateTime nowUtc)
    {
        RequireActive();
        RequireVersion(expectedVersion);
        RequireUtc(nowUtc, nameof(nowUtc));
        if (nowUtc < CreatedAtUtc)
            throw new ArgumentOutOfRangeException(nameof(nowUtc));

        ArchivedAtUtc = nowUtc;
        Version++;
    }

    public void Restore(long expectedVersion)
    {
        RequireVersion(expectedVersion);
        if (ArchivedAtUtc is null)
            throw new InvalidOperationException("Workspace is not archived.");

        ArchivedAtUtc = null;
        Version++;
    }

    private void SetInformation(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 150 ||
            name.Any(char.IsControl))
            throw new ArgumentException("Invalid workspace name.", nameof(name));

        if (description is not null &&
            (description.Length > 1000 || description.Any(char.IsControl)))
            throw new ArgumentException("Invalid description.", nameof(description));

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    private static string ValidateSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug) || slug.Length is < 3 or > 80 ||
            !SlugPattern.IsMatch(slug))
            throw new ArgumentException("Slug must be 3-80 lowercase ASCII characters and hyphens.", nameof(slug));

        return slug; // Do not silently rename identity-bearing slugs.
    }

    private void RequireActive()
    {
        if (ArchivedAtUtc.HasValue)
            throw new InvalidOperationException("Workspace is archived.");
    }

    private void RequireVersion(long expected)
    {
        if (expected < 1 || expected != Version || Version == long.MaxValue)
            throw new InvalidOperationException("Workspace version mismatch.");
    }

    internal static void RequireUtc(DateTime value, string parameterName)
    {
        if (value.Kind != DateTimeKind.Utc)
            throw new ArgumentException("Timestamp must be UTC.", parameterName);
    }
}
