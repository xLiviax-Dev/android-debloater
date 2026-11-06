using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>Zerlegt die Ausgabe von "adb shell getprop" in Gerätedaten.</summary>
public static class DeviceInfoReader
{
    /// <summary>
    /// Liest die Eckdaten aus der getprop-Ausgabe. Fehlt eine Eigenschaft,
    /// bleibt der Wert null – es wird nichts geraten.
    /// </summary>
    public static DeviceInfo Parse(string getPropOutput, string serial)
    {
        var values = ParseProperties(getPropOutput);

        return new DeviceInfo(
            Serial: serial,
            Manufacturer: FirstValue(values, "ro.product.manufacturer", "ro.product.vendor.manufacturer", "ro.product.brand"),
            Model: FirstValue(values, "ro.product.model", "ro.product.vendor.model", "ro.product.device"),
            AndroidVersion: FirstValue(values, "ro.build.version.release"),
            SdkLevel: ParseInt(FirstValue(values, "ro.build.version.sdk")),
            BuildId: FirstValue(values, "ro.build.id"));
    }

    /// <summary>Zerlegt Zeilen der Form "[eigenschaft]: [wert]".</summary>
    private static Dictionary<string, string> ParseProperties(string output)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(output))
        {
            return values;
        }

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length < 4 || line[0] != '[')
            {
                continue;
            }

            var keyEnd = line.IndexOf(']');
            if (keyEnd <= 1)
            {
                continue;
            }

            var rest = line[(keyEnd + 1)..].TrimStart();
            if (rest.Length < 2 || rest[0] != ':')
            {
                continue;
            }

            rest = rest[1..].Trim();
            if (rest.Length < 2 || rest[0] != '[')
            {
                continue;
            }

            var valueEnd = rest.LastIndexOf(']');
            if (valueEnd <= 0)
            {
                continue;
            }

            var key = line[1..keyEnd];
            var value = rest[1..valueEnd];
            if (value.Length > 0)
            {
                values[key] = value;
            }
        }

        return values;
    }

    private static string? FirstValue(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static int? ParseInt(string? text)
        => int.TryParse(text, out var value) ? value : null;
}
