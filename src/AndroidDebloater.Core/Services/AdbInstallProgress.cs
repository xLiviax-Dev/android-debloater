namespace AndroidDebloater.Core.Services;

/// <summary>Abschnitte des ADB-Installationsvorgangs.</summary>
public enum AdbInstallStage
{
    /// <summary>Version + Prüfsumme werden aus dem Google-Manifest gelesen.</summary>
    ReadingManifest,

    /// <summary>Die versionierte ZIP wird geladen.</summary>
    Downloading,

    /// <summary>Die SHA-1-Prüfsumme wird geprüft.</summary>
    VerifyingChecksum,

    /// <summary>Das Archiv wird entpackt.</summary>
    Extracting,

    /// <summary>Die Installation wird mit "adb --version" geprüft.</summary>
    VerifyingInstallation,

    /// <summary>Fertig.</summary>
    Completed,
}

/// <summary>Fortschritt des ADB-Installationsvorgangs.</summary>
/// <param name="Stage">Aktueller Abschnitt.</param>
/// <param name="BytesReceived">Bisher geladene Bytes (nur beim Download).</param>
/// <param name="TotalBytes">Erwartete Gesamtbytes (nur beim Download; 0 = unbekannt).</param>
public sealed record AdbInstallProgress(
    AdbInstallStage Stage,
    long BytesReceived = 0,
    long TotalBytes = 0);
