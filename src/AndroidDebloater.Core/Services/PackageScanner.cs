using System.Globalization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Liest den vollständigen Paketbestand des Zielgeräts aus:
/// <c>pm list packages -f -u</c> (Pfade), <c>dumpsys package</c> (Details und
/// Zustand) sowie <c>dumpsys diskstats</c> (Größen). Es werden ausschließlich
/// echte Geräteantworten ausgewertet – es wird nichts geraten.
/// </summary>
public static class PackageScanner
{
    private static readonly TimeSpan ListTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan LauncherTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GameTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan DumpTimeout = TimeSpan.FromMinutes(4);
    private static readonly TimeSpan DiskTimeout = TimeSpan.FromMinutes(2);

    /// <summary>Scannt alle Pakete des Zielgeräts in mehreren Abschnitten.</summary>
    public static async Task<IReadOnlyList<AndroidPackage>> ScanAsync(
        AdbRunner adb,
        IProgress<PackageScanProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adb);

        progress?.Report(new PackageScanProgress(PackageScanStage.ListingPackages));
        var listResult = await adb
            .RunShellAsync(new[] { "pm", "list", "packages", "-f", "-u" }, ListTimeout, cancellationToken)
            .ConfigureAwait(false);
        var pathFallback = ParsePackagePaths(listResult.StandardOutput);

        progress?.Report(new PackageScanProgress(PackageScanStage.ReadingLauncher));
        var launcherResult = await adb
            .RunShellAsync(
                new[]
                {
                    "cmd", "package", "query-activities", "--brief",
                    "-a", "android.intent.action.MAIN",
                    "-c", "android.intent.category.LAUNCHER",
                },
                LauncherTimeout,
                cancellationToken)
            .ConfigureAwait(false);
        var launcherPackages = ParseLauncherPackages(launcherResult.StandardOutput);

        progress?.Report(new PackageScanProgress(PackageScanStage.ReadingGames));
        var gameResult = await adb
            .RunShellAsync(new[] { "dumpsys", "game" }, GameTimeout, cancellationToken)
            .ConfigureAwait(false);
        // Das Geraet meldet nur installierte Spiele; die lokale Liste schreibt
        // erkannte Spiele fort, damit entfernte Spiele Spiele bleiben.
        var gamePackages = GameRegistry.Merge(ParseGamePackages(gameResult.StandardOutput));

        progress?.Report(new PackageScanProgress(PackageScanStage.ReadingDetails));
        var dumpResult = await adb
            .RunShellAsync(new[] { "dumpsys", "package" }, DumpTimeout, cancellationToken)
            .ConfigureAwait(false);
        var packages = ParseDump(dumpResult.StandardOutput, pathFallback);

        progress?.Report(new PackageScanProgress(PackageScanStage.ReadingSizes));
        var diskResult = await adb
            .RunShellAsync(new[] { "dumpsys", "diskstats" }, DiskTimeout, cancellationToken)
            .ConfigureAwait(false);
        var sizes = DiskStatsParser.Parse(diskResult.StandardOutput);

        foreach (var package in packages)
        {
            package.HasLauncherEntry = launcherPackages.Contains(package.PackageName);
            package.IsGame = gamePackages.Contains(package.PackageName);

            if (sizes.TryGetValue(package.PackageName, out var size))
            {
                package.ApkSizeBytes = size.ApkBytes;
                package.DataSizeBytes = size.DataBytes;
                package.CacheSizeBytes = size.CacheBytes;
            }

            BloatwareClassifier.Assess(package);
        }

        progress?.Report(new PackageScanProgress(PackageScanStage.Completed, packages.Count));
        return packages;
    }

    /// <summary>Aktualisiert Zustand und Details eines einzelnen Pakets nach einer Aktion.</summary>
    public static async Task RefreshStateAsync(
        AdbRunner adb,
        AndroidPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adb);
        ArgumentNullException.ThrowIfNull(package);

        var dumpResult = await adb
            .RunShellAsync(new[] { "dumpsys", "package", package.PackageName }, TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);

        var fresh = ParseDump(dumpResult.StandardOutput)
            .FirstOrDefault(p => string.Equals(p.PackageName, package.PackageName, StringComparison.Ordinal));

        if (fresh is null)
        {
            // "Unable to find package: ..." bedeutet: das Geraet fuehrt das
            // Paket nicht mehr (z. B. nach dem Loeschen der Restdaten, S-21).
            if (dumpResult.StandardOutput.Contains("Unable to find package", StringComparison.OrdinalIgnoreCase))
            {
                package.ExistsOnDevice = false;
            }

            return;
        }

        package.ExistsOnDevice = true;
        package.CodePath = fresh.CodePath ?? package.CodePath;
        package.Kind = fresh.Kind;
        package.IsPrivilegedApp = fresh.IsPrivilegedApp;
        package.Installed = fresh.Installed;
        package.Hidden = fresh.Hidden;
        package.Suspended = fresh.Suspended;
        package.Stopped = fresh.Stopped;
        package.EnabledState = fresh.EnabledState;
        package.InstallerPackage = fresh.InstallerPackage;
        package.FirstInstallTime = fresh.FirstInstallTime;
        package.VersionName = fresh.VersionName;

        BloatwareClassifier.Assess(package);
    }

    /// <summary>
    /// Zerlegt die Ausgabe von "pm list packages -f" in eine Zuordnung
    /// Paketname zu APK-Pfad.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ParsePackagePaths(string output)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(output))
        {
            return map;
        }

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("package:", StringComparison.Ordinal))
            {
                continue;
            }

            var rest = line["package:".Length..];

            // Der Pfad kann "==" enthalten (base64-Ordnernamen unter /data/app),
            // deshalb wird am LETZTEN "=" getrennt.
            var separator = rest.LastIndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var path = rest[..separator];
            var name = rest[(separator + 1)..];
            if (path.Length == 0 || name.Length == 0)
            {
                continue;
            }

            map[name] = path;
        }

        return map;
    }

    /// <summary>
    /// Zerlegt die Ausgabe von
    /// "cmd package query-activities --brief -a android.intent.action.MAIN
    /// -c android.intent.category.LAUNCHER" in die Menge der Pakete mit
    /// Launcher-Eintrag. Verwertet werden ausschließlich die Komponentenzeilen
    /// im Format "paketname/aktivität"; Kopf- ("52 activities found:"),
    /// Marker- ("Activity #0:") und Prioritätszeilen werden übersprungen.
    /// </summary>
    public static IReadOnlyCollection<string> ParseLauncherPackages(string output)
    {
        var packages = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(output))
        {
            return packages;
        }

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();

            // Nur Komponentenzeilen sind ohne Leerzeichen; alle anderen Zeilen
            // (Kopf, "Activity #N:", "priority=...") enthalten Leerzeichen.
            if (line.Length == 0 || line.IndexOf(' ') >= 0)
            {
                continue;
            }

            var slash = line.IndexOf('/');
            if (slash <= 0)
            {
                continue;
            }

            var packageName = line[..slash];
            if (IsPackageName(packageName))
            {
                packages.Add(packageName);
            }
        }

        return packages;
    }

    /// <summary>
    /// Zerlegt die Ausgabe von "dumpsys game" (GameManagerService, Android 12+)
    /// in die Menge der Spiele-Pakete. Das Gerät führt dort jedes Paket mit dem
    /// ApplicationInfo-Flag IS_GAME, z. B.
    /// "No intervention found for package com.ludia.jurassicworld". Es wird
    /// ausschließlich das Wort nach "package " verwertet.
    /// </summary>
    public static IReadOnlyCollection<string> ParseGamePackages(string output)
    {
        const string marker = "package ";
        var packages = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrEmpty(output))
        {
            return packages;
        }

        foreach (var raw in output.Split('\n'))
        {
            var markerIndex = raw.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
            {
                continue;
            }

            var name = raw[(markerIndex + marker.Length)..].Trim();
            var space = name.IndexOf(' ');
            if (space >= 0)
            {
                name = name[..space];
            }

            if (IsPackageName(name))
            {
                packages.Add(name);
            }
        }

        return packages;
    }

    /// <summary>Prüft grob, ob ein Text wie ein Android-Paketname aussieht.</summary>
    private static bool IsPackageName(string value)
    {
        if (value.Length == 0 || value.IndexOf('.') < 0)
        {
            return false;
        }

        foreach (var character in value)
        {
            if (!(char.IsLetterOrDigit(character) || character == '.' || character == '_'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Zerlegt die Ausgabe von "dumpsys package" in Pakete. Doppelte Einträge
    /// (aktualisierte System-Apps) werden zu einem Eintrag zusammengefasst.
    /// </summary>
    public static IReadOnlyList<AndroidPackage> ParseDump(
        string dump,
        IReadOnlyDictionary<string, string>? pathFallback = null)
    {
        var packages = new List<AndroidPackage>();
        if (string.IsNullOrEmpty(dump))
        {
            return packages;
        }

        AndroidPackage? current = null;
        var flagsCaptured = false;

        foreach (var raw in dump.Split('\n'))
        {
            var trimmed = raw.Trim('\r', ' ', '\t');
            if (trimmed.Length == 0)
            {
                continue;
            }

            if (trimmed.StartsWith("Package [", StringComparison.Ordinal))
            {
                if (current is not null)
                {
                    Finalize(current, flagsCaptured);
                    packages.Add(current);
                }

                var start = trimmed.IndexOf('[') + 1;
                var end = trimmed.IndexOf(']');
                var name = end > start ? trimmed[start..end] : string.Empty;

                current = new AndroidPackage { PackageName = name };
                flagsCaptured = false;

                if (pathFallback is not null && pathFallback.TryGetValue(name, out var fallbackPath))
                {
                    current.CodePath = fallbackPath;
                }

                continue;
            }

            if (current is null)
            {
                continue;
            }

            if (trimmed.StartsWith("codePath=", StringComparison.Ordinal))
            {
                current.CodePath = trimmed["codePath=".Length..].Trim();
            }
            else if (!flagsCaptured && trimmed.StartsWith("flags=[", StringComparison.Ordinal))
            {
                flagsCaptured = true;
                current.Kind = HasFlagToken(trimmed, "SYSTEM") || HasFlagToken(trimmed, "UPDATED_SYSTEM_APP")
                    ? PackageKind.System
                    : PackageKind.User;
            }
            else if (trimmed.StartsWith("privateFlags=[", StringComparison.Ordinal))
            {
                if (HasFlagToken(trimmed, "PRIVILEGED"))
                {
                    current.IsPrivilegedApp = true;
                }
            }
            else if (trimmed.StartsWith("installerPackageName=", StringComparison.Ordinal))
            {
                current.InstallerPackage = NormalizeValue(trimmed["installerPackageName=".Length..]);
            }
            else if (trimmed.StartsWith("firstInstallTime=", StringComparison.Ordinal))
            {
                var value = trimmed["firstInstallTime=".Length..].Trim();
                if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    current.FirstInstallTime = date;
                }
            }
            else if (trimmed.StartsWith("versionName=", StringComparison.Ordinal))
            {
                current.VersionName = NormalizeValue(trimmed["versionName=".Length..]);
            }
            else if (trimmed.StartsWith("User 0:", StringComparison.Ordinal))
            {
                ApplyUserState(trimmed, current);
            }
        }

        if (current is not null)
        {
            Finalize(current, flagsCaptured);
            packages.Add(current);
        }

        return Deduplicate(packages);
    }

    /// <summary>
    /// Vervollständigt die Klassifizierung: Fehlt das flags-Feld, wird die
    /// Herkunft aus dem Code-Pfad abgeleitet; privilegierte Apps werden
    /// zusätzlich am Pfad erkannt.
    /// </summary>
    private static void Finalize(AndroidPackage package, bool kindFromFlags)
    {
        if (!kindFromFlags)
        {
            package.Kind = IsSystemPath(package.CodePath) ? PackageKind.System : PackageKind.User;
        }

        if (!package.IsPrivilegedApp
            && package.CodePath?.Contains("/priv-app/", StringComparison.Ordinal) == true)
        {
            package.IsPrivilegedApp = true;
        }
    }

    /// <summary>
    /// "dumpsys package" listet aktualisierte System-Apps zweimal: die aktive
    /// Version aus /data/app und die werkseitige Version aus /system bzw.
    /// /product. Pro Paketname wird ein Eintrag behalten – bevorzugt die
    /// aktive Version.
    /// </summary>
    private static IReadOnlyList<AndroidPackage> Deduplicate(List<AndroidPackage> packages)
    {
        var result = new List<AndroidPackage>(packages.Count);
        var indexByName = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var package in packages)
        {
            if (indexByName.TryGetValue(package.PackageName, out var index))
            {
                if (IsActiveVersion(package) && !IsActiveVersion(result[index]))
                {
                    result[index] = package;
                }

                continue;
            }

            indexByName[package.PackageName] = result.Count;
            result.Add(package);
        }

        return result;
    }

    private static bool IsActiveVersion(AndroidPackage package)
        => package.CodePath?.StartsWith("/data/app/", StringComparison.Ordinal) == true;

    private static bool IsSystemPath(string? codePath)
    {
        if (string.IsNullOrEmpty(codePath))
        {
            return false;
        }

        return codePath.StartsWith("/system/", StringComparison.Ordinal)
            || codePath.StartsWith("/system_ext/", StringComparison.Ordinal)
            || codePath.StartsWith("/product/", StringComparison.Ordinal)
            || codePath.StartsWith("/vendor/", StringComparison.Ordinal)
            || codePath.StartsWith("/odm/", StringComparison.Ordinal)
            || codePath.StartsWith("/apex/", StringComparison.Ordinal);
    }

    /// <summary>Liest die Zustandsmerkmale aus der "User 0:"-Zeile.</summary>
    private static void ApplyUserState(string line, AndroidPackage package)
    {
        foreach (var token in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = token.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = token[..separator];
            var value = token[(separator + 1)..];

            switch (key)
            {
                case "installed":
                    package.Installed = value == "true";
                    break;
                case "hidden":
                    package.Hidden = value == "true";
                    break;
                case "suspended":
                    package.Suspended = value == "true";
                    break;
                case "stopped":
                    package.Stopped = value == "true";
                    break;
                case "enabled":
                    if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var state))
                    {
                        package.EnabledState = state;
                    }

                    break;
            }
        }
    }

    /// <summary>Prüft, ob ein Token in "flags=[ ... ]" / "privateFlags=[ ... ]" steht.</summary>
    private static bool HasFlagToken(string line, string token)
    {
        var open = line.IndexOf('[');
        var close = line.LastIndexOf(']');
        if (open < 0 || close <= open)
        {
            return false;
        }

        foreach (var candidate in line[(open + 1)..close].Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (string.Equals(candidate, token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Wandelt leere Werte und "null" in null um.</summary>
    private static string? NormalizeValue(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 || string.Equals(trimmed, "null", StringComparison.Ordinal)
            ? null
            : trimmed;
    }
}
