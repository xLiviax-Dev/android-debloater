using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Sucht eine vorhandene ADB-Installation in fester Reihenfolge. Es wird
/// nichts installiert; das übernimmt <see cref="AdbInstaller"/>.
/// Reihenfolge (übernommen aus dem Prototyp, P3-1):
/// 1) portabel neben der Anwendung, 2) Umgebungsvariablen, 3) PATH,
/// 4) übliche SDK-Pfade, 5) winget, 6) von der Anwendung verwaltet.
/// </summary>
public static class AdbLocator
{
    /// <summary>Sucht adb und liefert Pfad + Quelle oder null.</summary>
    public static (string Path, AdbSource Source)? FindAdb()
    {
        foreach (var candidate in EnumerateCandidates())
        {
            if (File.Exists(candidate.Path))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>
    /// Liefert alle möglichen adb-Pfade in Suchreihenfolge (auch nicht
    /// existierende) – ohne Duplikate.
    /// </summary>
    public static IEnumerable<(string Path, AdbSource Source)> EnumerateCandidates()
    {
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var seen = new HashSet<string>(comparer);

        foreach (var candidate in EnumerateRaw())
        {
            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(candidate.Path);
            }
            catch (Exception)
            {
                continue;
            }

            if (seen.Add(fullPath))
            {
                yield return (fullPath, candidate.Source);
            }
        }
    }

    private static IEnumerable<(string Path, AdbSource Source)> EnumerateRaw()
    {
        // 1) portabel neben der Anwendung
        yield return (
            Path.Combine(AppContext.BaseDirectory, "platform-tools", AppPaths.AdbExecutableName),
            AdbSource.Portable);

        // 2) Umgebungsvariablen
        foreach (var candidate in FromEnvironment("ADB"))
        {
            yield return candidate;
        }

        foreach (var candidate in FromSdkRoot(Environment.GetEnvironmentVariable("ANDROID_HOME")))
        {
            yield return candidate;
        }

        foreach (var candidate in FromSdkRoot(Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT")))
        {
            yield return candidate;
        }

        // 3) PATH
        var pathValue = Environment.GetEnvironmentVariable("PATH");
        if (!string.IsNullOrWhiteSpace(pathValue))
        {
            foreach (var directory in pathValue.Split(Path.PathSeparator))
            {
                if (string.IsNullOrWhiteSpace(directory))
                {
                    continue;
                }

                yield return (Path.Combine(directory, AppPaths.AdbExecutableName), AdbSource.Path);
            }
        }

        // 4) übliche SDK-Pfade
        foreach (var candidate in CommonLocations())
        {
            yield return candidate;
        }

        // 5) winget
        foreach (var candidate in WingetPackages())
        {
            yield return candidate;
        }

        // 6) von der Anwendung verwaltet
        yield return (AppPaths.ManagedAdbPath, AdbSource.AppManaged);
    }

    private static IEnumerable<(string Path, AdbSource Source)> FromEnvironment(string variable)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        // Die Variable darf entweder auf die adb-Datei oder auf einen Ordner zeigen.
        yield return Directory.Exists(value)
            ? (Path.Combine(value, AppPaths.AdbExecutableName), AdbSource.EnvironmentVariable)
            : (value, AdbSource.EnvironmentVariable);
    }

    private static IEnumerable<(string Path, AdbSource Source)> FromSdkRoot(string? root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            yield break;
        }

        yield return (
            Path.Combine(root, "platform-tools", AppPaths.AdbExecutableName),
            AdbSource.EnvironmentVariable);
    }

    private static IEnumerable<(string Path, AdbSource Source)> CommonLocations()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            yield return (
                Path.Combine(localAppData, "Android", "Sdk", "platform-tools", AppPaths.AdbExecutableName),
                AdbSource.CommonLocation);
        }

        if (OperatingSystem.IsWindows())
        {
            yield return (
                Path.Combine(@"C:\platform-tools", AppPaths.AdbExecutableName),
                AdbSource.CommonLocation);

            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return (
                    Path.Combine(programFiles, "platform-tools", AppPaths.AdbExecutableName),
                    AdbSource.CommonLocation);
            }
        }
    }

    private static IEnumerable<(string Path, AdbSource Source)> WingetPackages()
    {
        if (!OperatingSystem.IsWindows())
        {
            yield break;
        }

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            yield break;
        }

        var packagesFolder = Path.Combine(localAppData, "Microsoft", "WinGet", "Packages");
        if (!Directory.Exists(packagesFolder))
        {
            yield break;
        }

        foreach (var packageFolder in Directory.EnumerateDirectories(packagesFolder, "Google.PlatformTools*"))
        {
            yield return (
                Path.Combine(packageFolder, "platform-tools", AppPaths.AdbExecutableName),
                AdbSource.Winget);
        }
    }
}
