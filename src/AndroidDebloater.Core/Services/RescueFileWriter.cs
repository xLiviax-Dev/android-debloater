using System.Text;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Schreibt die Rettungs-Datei: eine Textdatei mit den Wiederherstellungs-
/// Befehlen zum manuellen Kopieren, falls die App nicht mehr erreichbar ist
/// (projektinfo.txt S-05).
/// </summary>
public static class RescueFileWriter
{
    /// <summary>Dateiname der Rettungs-Datei.</summary>
    public static string CreateFileName(DateTimeOffset timestamp)
        => $"rescue-{timestamp:yyyyMMdd-HHmmss}.txt";

    /// <summary>Erzeugt den Inhalt der Rettungs-Datei für ein Profil.</summary>
    public static string BuildText(RollbackProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var builder = new StringBuilder();
        builder.AppendLine("Android Debloater - rescue file");
        builder.AppendLine("Restores the packages that were changed in this session.");
        builder.AppendLine("Run the commands below one by one (adb must be on PATH):");
        builder.AppendLine();

        if (!string.IsNullOrWhiteSpace(profile.Device))
        {
            builder.AppendLine($"Device: {profile.Device}");
        }

        builder.AppendLine($"Created: {profile.CreatedAt:yyyy-MM-dd HH:mm:ss}");
        builder.AppendLine();

        if (profile.Entries.Count == 0)
        {
            builder.AppendLine("(no actions recorded)");
            return builder.ToString();
        }

        foreach (var entry in profile.Entries)
        {
            var label = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.PackageName
                : $"{entry.PackageName} ({entry.DisplayName})";

            builder.AppendLine($"# {label}");
            builder.AppendLine($"# was done: adb shell {entry.CommandText}");
            if (entry.IsPermanent)
            {
                builder.AppendLine("# not restorable via ADB (S-21) - opens the Play Store page:");
            }

            builder.AppendLine($"adb shell {entry.RestoreCommandText}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    /// <summary>Schreibt die Rettungs-Datei an den angegebenen Pfad.</summary>
    public static async Task WriteAsync(
        RollbackProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, BuildText(profile), cancellationToken).ConfigureAwait(false);
    }
}
