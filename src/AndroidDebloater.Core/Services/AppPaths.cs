namespace AndroidDebloater.Core.Services;

/// <summary>
/// Zentrale Pfade der Anwendung. Alles, was die Anwendung selbst ablegt,
/// liegt unter <c>%LOCALAPPDATA%\Android Debloater</c>.
/// </summary>
public static class AppPaths
{
    /// <summary>Name des Ordners unterhalb von %LOCALAPPDATA%.</summary>
    public const string AppFolderName = "Android Debloater";

    /// <summary>Dateiname der ADB-Ausführungsdatei für das aktuelle System.</summary>
    public static string AdbExecutableName
        => OperatingSystem.IsWindows() ? "adb.exe" : "adb";

    /// <summary>Basispfad für anwendungsbezogene Daten des Nutzers.</summary>
    public static string LocalAppDataFolder
        => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    /// <summary>Ordner der Anwendung unterhalb von %LOCALAPPDATA%.</summary>
    public static string AppFolder
        => Path.Combine(LocalAppDataFolder, AppFolderName);

    /// <summary>Zielordner der von der Anwendung verwalteten platform-tools.</summary>
    public static string PlatformToolsFolder
        => Path.Combine(AppFolder, "platform-tools");

    /// <summary>Ordner für die zwischengespeicherten App-Icons und Anzeigenamen.</summary>
    public static string IconCacheFolder
        => Path.Combine(AppFolder, "icons");

    /// <summary>Ordner für die gesicherten Wiederherstellungs-Profile (S-04).</summary>
    public static string RollbackFolder
        => Path.Combine(AppFolder, "rollback");

    /// <summary>Pfad der fortgeschriebenen Aktions-Protokolldatei (S-14).</summary>
    public static string ActionLogFile
        => Path.Combine(AppFolder, "actions.log");

    /// <summary>Pfad der gespeicherten Nutzereinstellungen als JSON (M8).</summary>
    public static string SettingsFile
        => Path.Combine(AppFolder, "settings.json");

    /// <summary>Zielordner der Paket-Exporte als CSV/Markdown/HTML (M7 / F-09).</summary>
    public static string ExportFolder
        => Path.Combine(AppFolder, "export");

    /// <summary>Pfad zur von der Anwendung verwalteten adb-Datei.</summary>
    public static string ManagedAdbPath
        => Path.Combine(PlatformToolsFolder, AdbExecutableName);
}
