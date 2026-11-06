namespace AndroidDebloater.Core.Models;

/// <summary>Ziel-Betriebssystem eines platform-tools-Archivs.</summary>
public enum HostOperatingSystem
{
    Windows,
    MacOs,
    Linux,
}

/// <summary>
/// Ein einzelnes platform-tools-Archiv aus dem offiziellen
/// Google-SDK-Manifest (eine ZIP je Host-Betriebssystem).
/// </summary>
/// <param name="HostOs">Ziel-Betriebssystem.</param>
/// <param name="FileName">Dateiname der ZIP (z. B. "platform-tools_r37.0.1-win.zip").</param>
/// <param name="SizeBytes">Dateigröße laut Manifest.</param>
/// <param name="Sha1">Offizielle SHA-1-Prüfsumme (40 Hex-Zeichen) laut Manifest.</param>
public sealed record AdbReleaseArchive(
    HostOperatingSystem HostOs,
    string FileName,
    long SizeBytes,
    string Sha1);

/// <summary>
/// Eine platform-tools-Version mit allen zugehörigen Archiven, wie sie aus
/// dem offiziellen SDK-Manifest (repository2-1.xml) gelesen wurde.
/// </summary>
/// <param name="Version">Versionsnummer, z. B. "37.0.1".</param>
/// <param name="Revision">Revisionsnummer aus dem Manifest, z. B. "37.0.1".</param>
/// <param name="Archives">Archivliste je Host-Betriebssystem.</param>
public sealed record PlatformToolsRelease(
    string Version,
    string Revision,
    IReadOnlyList<AdbReleaseArchive> Archives)
{
    /// <summary>Liefert das Archiv für das angegebene Betriebssystem oder null.</summary>
    public AdbReleaseArchive? ForHost(HostOperatingSystem hostOs)
        => Archives.FirstOrDefault(a => a.HostOs == hostOs);
}

/// <summary>Hilfsfunktionen rund um das Host-Betriebssystem.</summary>
public static class HostOperatingSystemExtensions
{
    /// <summary>Das Betriebssystem, auf dem die Anwendung gerade läuft.</summary>
    public static HostOperatingSystem Current
        => OperatingSystem.IsWindows() ? HostOperatingSystem.Windows
         : OperatingSystem.IsMacOS() ? HostOperatingSystem.MacOs
         : HostOperatingSystem.Linux;

    /// <summary>Der im Google-Manifest verwendete Schlüssel (host-os).</summary>
    public static string ManifestToken(this HostOperatingSystem hostOs) => hostOs switch
    {
        HostOperatingSystem.Windows => "windows",
        HostOperatingSystem.MacOs => "macosx",
        HostOperatingSystem.Linux => "linux",
        _ => throw new ArgumentOutOfRangeException(nameof(hostOs)),
    };
}
