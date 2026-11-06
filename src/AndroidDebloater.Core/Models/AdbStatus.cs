namespace AndroidDebloater.Core.Models;

/// <summary>
/// Ergebnis der ADB-Erkennung: ob adb gefunden wurde, wo es liegt,
/// welche Version es meldet und aus welcher Quelle es stammt.
/// </summary>
/// <param name="IsFound">True, wenn eine ausführbare adb-Datei gefunden wurde.</param>
/// <param name="ExecutablePath">Vollständiger Pfad zur adb-Datei (null, wenn nicht gefunden).</param>
/// <param name="Version">Kurzversion der platform-tools, z. B. "37.0.1" (kann null sein).</param>
/// <param name="Source">Quelle, über die adb gefunden wurde.</param>
/// <param name="VersionText">Vollständige Ausgabe von "adb --version" (kann null sein).</param>
public sealed record AdbStatus(
    bool IsFound,
    string? ExecutablePath,
    string? Version,
    AdbSource Source,
    string? VersionText = null)
{
    /// <summary>Status für "kein ADB gefunden".</summary>
    public static AdbStatus NotFound { get; } =
        new(false, null, null, AdbSource.Unknown);
}
