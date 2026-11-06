namespace AndroidDebloater.Core.Services;

/// <summary>Offizielle Google-Endpunkte für die ADB-Beschaffung (Abschnitt 4.1).</summary>
public static class AdbEndpoints
{
    /// <summary>Offizielles SDK-Repo-Manifest (enthält Version + SHA-1 je Datei).</summary>
    public const string SdkRepositoryManifest
        = "https://dl.google.com/android/repository/repository2-1.xml";

    /// <summary>Basis-URL für die versionierten Dateien aus dem Manifest.</summary>
    public const string DownloadBaseUrl
        = "https://dl.google.com/android/repository/";

    /// <summary>Paketpfad der platform-tools im Manifest.</summary>
    public const string PlatformToolsPackagePath = "platform-tools";

    /// <summary>Bildet die Download-URL für einen Manifest-Dateinamen.</summary>
    public static string DownloadUrl(string fileName) => DownloadBaseUrl + fileName;
}
