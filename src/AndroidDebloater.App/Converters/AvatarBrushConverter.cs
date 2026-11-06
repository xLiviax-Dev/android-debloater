using System;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace AndroidDebloater.App.Converters;

/// <summary>
/// Liefert zu einem Paketnamen eine feste Farbe aus einer kleinen Palette, damit
/// Apps ohne Icon einen wiedererkennbaren farbigen Buchstaben-Kreis bekommen.
/// ConverterParameter "bg" = transparenter Hintergrund, sonst die Schriftfarbe.
/// Die Farben sind mittlere Töne und wirken in hellem wie dunklem Schema.
/// </summary>
public sealed class AvatarBrushConverter : IValueConverter
{
    public static readonly AvatarBrushConverter Instance = new();

    private static readonly Color[] Palette =
    {
        Color.Parse("#4C78F0"),
        Color.Parse("#E8742A"),
        Color.Parse("#1E9B5A"),
        Color.Parse("#9B5BE0"),
        Color.Parse("#E0457B"),
        Color.Parse("#1AA3B8"),
    };

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var key = value as string ?? string.Empty;
        var hash = 17;
        foreach (var c in key)
        {
            hash = unchecked((hash * 31) + c);
        }

        var color = Palette[(hash & 0x7FFFFFFF) % Palette.Length];
        return string.Equals(parameter as string, "bg", StringComparison.Ordinal)
            ? new SolidColorBrush(Color.FromArgb(0x33, color.R, color.G, color.B))
            : new SolidColorBrush(color);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
