namespace AndroidDebloater.Core.Models;

/// <summary>
/// Woher eine gefundene ADB-Installation stammt. Die Reihenfolge der
/// Auflistung entspricht der Suchreihenfolge (siehe AdbLocator).
/// </summary>
public enum AdbSource
{
    /// <summary>Unbekannt / nicht gefunden.</summary>
    Unknown = 0,

    /// <summary>platform-tools-Ordner liegt portabel neben der Anwendung.</summary>
    Portable,

    /// <summary>Über Umgebungsvariable (ADB, ANDROID_HOME oder ANDROID_SDK_ROOT).</summary>
    EnvironmentVariable,

    /// <summary>adb liegt im System-PATH.</summary>
    Path,

    /// <summary>Übliche SDK-Installationspfade (z. B. %LOCALAPPDATA%\Android\Sdk).</summary>
    CommonLocation,

    /// <summary>Von winget installiertes Google.PlatformTools-Paket.</summary>
    Winget,

    /// <summary>Von der Anwendung selbst heruntergeladen (%LOCALAPPDATA%).</summary>
    AppManaged,

    /// <summary>Vom Nutzer in den Einstellungen angegebener Pfad (M8).</summary>
    UserConfigured,
}
