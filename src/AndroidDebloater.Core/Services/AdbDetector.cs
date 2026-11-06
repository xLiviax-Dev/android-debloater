using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Ermittelt den ADB-Status: sucht adb (siehe <see cref="AdbLocator"/>) und
/// liest – falls gefunden – die Version über "adb --version" aus.
/// Ein in den Einstellungen angegebener Pfad hat Vorrang (M8).
/// </summary>
public static class AdbDetector
{
    /// <summary>Sucht adb und liefert den vollständigen Status.</summary>
    /// <param name="preferredPath">
    /// Optionaler, vom Nutzer angegebener Pfad zur adb-Datei. Existiert er,
    /// wird er statt der automatischen Suche verwendet (M8).
    /// </param>
    /// <param name="cancellationToken">Abbruch der Versionsabfrage.</param>
    public static async Task<AdbStatus> DetectAsync(
        string? preferredPath = null,
        CancellationToken cancellationToken = default)
    {
        (string Path, AdbSource Source)? found = null;

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            found = ResolvePreferred(preferredPath);
        }

        found ??= AdbLocator.FindAdb();

        if (found is null)
        {
            return AdbStatus.NotFound;
        }

        var (path, source) = found.Value;
        var runner = new AdbRunner(path);

        string versionText;
        try
        {
            versionText = await runner.GetVersionTextAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Datei gefunden, aber nicht ausführbar -> ehrlich als "ohne Version" melden.
            return new AdbStatus(true, path, null, source);
        }

        var version = AdbRunner.ParseVersion(versionText);
        return new AdbStatus(true, path, version, source, versionText);
    }

    /// <summary>
    /// Prüft den vom Nutzer angegebenen Pfad. Er darf auf die adb-Datei oder
    /// auf den platform-tools-Ordner zeigen; fehlt die Datei, wird die
    /// automatische Suche verwendet.
    /// </summary>
    private static (string Path, AdbSource Source)? ResolvePreferred(string preferredPath)
    {
        try
        {
            var candidate = File.Exists(preferredPath)
                ? preferredPath
                : Directory.Exists(preferredPath)
                    ? Path.Combine(preferredPath, AppPaths.AdbExecutableName)
                    : preferredPath;

            return File.Exists(candidate)
                ? (Path.GetFullPath(candidate), AdbSource.UserConfigured)
                : null;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
