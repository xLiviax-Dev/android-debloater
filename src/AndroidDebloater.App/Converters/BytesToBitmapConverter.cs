using System;
using System.Globalization;
using System.IO;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;

namespace AndroidDebloater.App.Converters;

/// <summary>
/// Wandelt zwischengespeicherte PNG-Daten in ein anzeigbares Bild um.
/// Ungültige Daten führen zu keiner Anzeige statt zu einem Absturz.
/// </summary>
public sealed class BytesToBitmapConverter : IValueConverter
{
    /// <summary>Gemeinsame Instanz für die Verwendung in XAML (x:Static).</summary>
    public static readonly BytesToBitmapConverter Instance = new();

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not byte[] { Length: > 0 } bytes)
        {
            return null;
        }

        try
        {
            using var stream = new MemoryStream(bytes, writable: false);
            return new Bitmap(stream);
        }
        catch
        {
            return null;
        }
    }

    /// <inheritdoc />
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
