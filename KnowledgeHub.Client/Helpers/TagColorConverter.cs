using System.Globalization;

namespace KnowledgeHub.Client.Helpers;

public class TagColorConverter
    : IValueConverter
{
    private static readonly string[] Colors =
    [
        "#2563EB",
        "#10B981",
        "#F59E0B",
        "#EC4899",
        "#8B5CF6",
        "#14B8A6"
    ];

    public object Convert(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        if (value is not string tag)
        {
            return Colors[0];
        }

        var index =
            Math.Abs(tag.GetHashCode())
            % Colors.Length;

        return Color.FromArgb(
            Colors[index]);
    }

    public object ConvertBack(
        object value,
        Type targetType,
        object parameter,
        CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}