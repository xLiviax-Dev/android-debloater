using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>Zerlegt die Ausgabe von "adb devices -l".</summary>
public static class AdbDeviceReader
{
    /// <summary>
    /// Liest alle gemeldeten Geräte. Kopfzeilen und Daemon-Hinweise
    /// (Zeilen mit "*") werden übersprungen.
    /// </summary>
    public static IReadOnlyList<AdbDevice> Parse(string output)
    {
        var devices = new List<AdbDevice>();
        if (string.IsNullOrEmpty(output))
        {
            return devices;
        }

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("List of devices", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.StartsWith('*'))
            {
                // z. B. "* daemon not running; starting now at tcp:5037"
                continue;
            }

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
            {
                continue;
            }

            var serial = parts[0];
            var state = parts[1];
            string? model = null;
            string? product = null;
            string? device = null;

            for (var i = 2; i < parts.Length; i++)
            {
                var token = parts[i];
                var colon = token.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var key = token[..colon];
                var value = token[(colon + 1)..];
                switch (key)
                {
                    case "model":
                        model = value;
                        break;
                    case "product":
                        product = value;
                        break;
                    case "device":
                        device = value;
                        break;
                }
            }

            devices.Add(new AdbDevice(serial, state, model, product, device));
        }

        return devices;
    }
}
