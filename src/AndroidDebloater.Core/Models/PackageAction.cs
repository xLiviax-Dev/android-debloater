namespace AndroidDebloater.Core.Models;

/// <summary>
/// Schreibende Aktion an einem Paket (projektinfo.txt Abschnitt 6.3 / M6 / M12).
/// Umkehrbare Aktionen haben einen Rückweg, die als "endgültig" gekennzeichneten
/// lassen sich per ADB nicht zurücknehmen – siehe
/// <see cref="Services.ActionPlanner"/>.
/// </summary>
public enum PackageAction
{
    /// <summary>System-App für Benutzer 0 entfernen; der Datenordner bleibt erhalten
    /// und der Rückweg ist "install-existing".</summary>
    UninstallForUser,

    /// <summary>Nachinstallierte App endgültig entfernen (ohne "-k"): App und Daten
    /// werden gelöscht. Es gibt keinen ADB-Rückweg – nur eine Neuinstallation über
    /// den Play Store.</summary>
    UninstallPermanent,

    /// <summary>Nachinstallierte App entfernen, aber Daten und Cache behalten
    /// ("-k"): Der APK wird gelöscht, die Daten bleiben für eine spätere
    /// Neuinstallation erhalten. Ebenfalls kein ADB-Rückweg.</summary>
    UninstallKeepData,

    /// <summary>
    /// Restdaten einer bereits entfernten nachinstallierten App löschen, damit
    /// sie vollständig vom Gerät verschwindet. Am TB311FU geprüft: nur
    /// "pm uninstall PACKAGE" OHNE "--user 0" räumt die mit "-k" behaltenen
    /// Daten weg ("Success"); mit "--user 0" kommt "Failure [not installed for
    /// 0]", "pm clear" scheitert und "install-existing" findet kein Paket mehr.
    /// Endgültig – es gibt keinen ADB-Rückweg (S-21).
    /// </summary>
    PurgeRemovedData,

    /// <summary>Nachinstallierte App für Benutzer 0 deaktivieren.</summary>
    DisableForUser,

    /// <summary>Entfernte System-App wiederherstellen.</summary>
    InstallExisting,

    /// <summary>Deaktivierte App wieder aktivieren.</summary>
    EnableForUser,

    /// <summary>
    /// Rettungsweg eines endgültigen Entfernens: Die Play-Store-Seite des Pakets
    /// wird auf dem Gerät geöffnet, damit die App von Hand neu installiert werden
    /// kann. Kein automatischer Rollback.
    /// </summary>
    ReinstallFromStore,
}
