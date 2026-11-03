namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Domain-only validation of workspace presentation metadata.
/// This is not an authorization or persistence boundary.
/// </summary>
internal static class WorkspacePresentationValidation
{
    internal static void RequireId(Guid value, string parameterName)
    {
        if (value == Guid.Empty)
            throw new ArgumentException("A nonempty identifier is required.", parameterName);
    }

    internal static string RequiredText(string? value, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength ||
            value.Any(char.IsControl))
            throw new ArgumentException("Invalid text value.", parameterName);

        var trimmed = value.Trim();
        if (trimmed.Length == 0 || trimmed.Length > maxLength)
            throw new ArgumentException("Invalid text value.", parameterName);

        return trimmed;
    }

    internal static string? OptionalText(string? value, int maxLength, string parameterName)
    {
        if (value is null)
            return null;
        if (value.Length > maxLength || value.Any(char.IsControl))
            throw new ArgumentException("Invalid text value.", parameterName);
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    internal static void RequireVersion(long actualVersion, long expectedVersion)
    {
        if (actualVersion < 1 || expectedVersion != actualVersion ||
            actualVersion == long.MaxValue)
            throw new InvalidOperationException("Workspace presentation version mismatch.");
    }

    internal static bool IsLowerHex64(string? value) =>
        value is { Length: 64 } &&
        value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
}
