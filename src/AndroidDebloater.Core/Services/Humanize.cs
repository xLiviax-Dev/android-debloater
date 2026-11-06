namespace AndroidDebloater.Core.Services;

/// <summary>Formatiert Byte-Größen lesbar (z. B. "8,0 MB").</summary>
public static class Humanize
{
    private static readonly string[] Units = { "B", "KB", "MB", "GB", "TB", "PB" };

    /// <summary>Wandelt Bytes in eine lesbare Zeichenkette um.</summary>
    public static string Bytes(long bytes)
    {
        if (bytes < 0)
        {
            return "-";
        }

        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{bytes} {Units[unit]}"
            : $"{value.ToString("0.#", System.Globalization.CultureInfo.CurrentCulture)} {Units[unit]}";
    }
}
