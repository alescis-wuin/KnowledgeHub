namespace KnowledgeHub.Domain.Entities;

/// <summary>
/// Immutable, validated palette. Only the three documented text/background
/// pairings are guaranteed to meet WCAG AA for normal text.
/// </summary>
public sealed class WorkspaceBrandingColors
{
    public string PrimaryColor { get; }
    public string SecondaryColor { get; }
    public string AccentColor { get; }
    public string BackgroundColor { get; }
    public string SurfaceColor { get; }
    public string TextColor { get; }
    public string TextOnPrimaryColor { get; }

    public WorkspaceBrandingColors(
        string primaryColor,
        string secondaryColor,
        string accentColor,
        string backgroundColor,
        string surfaceColor,
        string textColor,
        string textOnPrimaryColor)
    {
        PrimaryColor = Normalize(primaryColor, nameof(primaryColor));
        SecondaryColor = Normalize(secondaryColor, nameof(secondaryColor));
        AccentColor = Normalize(accentColor, nameof(accentColor));
        BackgroundColor = Normalize(backgroundColor, nameof(backgroundColor));
        SurfaceColor = Normalize(surfaceColor, nameof(surfaceColor));
        TextColor = Normalize(textColor, nameof(textColor));
        TextOnPrimaryColor = Normalize(textOnPrimaryColor, nameof(textOnPrimaryColor));

        if (ContrastRatio(TextColor, BackgroundColor) < 4.5 ||
            ContrastRatio(TextColor, SurfaceColor) < 4.5 ||
            ContrastRatio(TextOnPrimaryColor, PrimaryColor) < 4.5)
            throw new ArgumentException("Documented color pairs must meet WCAG AA contrast 4.5:1.");
    }

    public static WorkspaceBrandingColors Default() => new(
        "#2563EB", "#64748B", "#0D9488", "#F8FAFC",
        "#FFFFFF", "#0F172A", "#FFFFFF");

    private static string Normalize(string? value, string name)
    {
        if (value is not { Length: 7 } || value[0] != '#')
            throw new ArgumentException("Expected exactly #RRGGBB.", name);

        for (var i = 1; i < value.Length; i++)
        {
            var c = value[i];
            if (c is not (>= '0' and <= '9' or >= 'A' and <= 'F' or >= 'a' and <= 'f'))
                throw new ArgumentException("Expected exactly #RRGGBB.", name);
        }

        return value.ToUpperInvariant();
    }

    private static double ContrastRatio(string first, string second)
    {
        var a = Luminance(first);
        var b = Luminance(second);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    private static double Luminance(string hex)
    {
        static double Channel(int value)
        {
            var normalized = value / 255.0;
            return normalized <= 0.04045
                ? normalized / 12.92
                : Math.Pow((normalized + 0.055) / 1.055, 2.4);
        }

        var red = Convert.ToInt32(hex.Substring(1, 2), 16);
        var green = Convert.ToInt32(hex.Substring(3, 2), 16);
        var blue = Convert.ToInt32(hex.Substring(5, 2), 16);
        return 0.2126 * Channel(red) +
               0.7152 * Channel(green) +
               0.0722 * Channel(blue);
    }
}
