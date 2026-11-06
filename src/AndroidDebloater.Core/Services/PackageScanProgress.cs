namespace AndroidDebloater.Core.Services;

/// <summary>Abschnitte eines Paket-Scans.</summary>
public enum PackageScanStage
{
    /// <summary>Paketliste mit Pfaden wird gelesen (pm list packages).</summary>
    ListingPackages,

    /// <summary>Launcher-Einträge werden gelesen (cmd package query-activities).</summary>
    ReadingLauncher,

    /// <summary>Spiele werden ermittelt (dumpsys game).</summary>
    ReadingGames,

    /// <summary>Paketdetails werden gelesen (dumpsys package).</summary>
    ReadingDetails,

    /// <summary>Speichergrößen werden gelesen (dumpsys diskstats).</summary>
    ReadingSizes,

    /// <summary>Scan abgeschlossen.</summary>
    Completed,
}

/// <summary>Fortschrittsmeldung während eines Paket-Scans.</summary>
/// <param name="Stage">Aktueller Abschnitt.</param>
/// <param name="PackageCount">Anzahl der Pakete (nur bei <see cref="PackageScanStage.Completed"/>).</param>
public sealed record PackageScanProgress(PackageScanStage Stage, int? PackageCount = null);
