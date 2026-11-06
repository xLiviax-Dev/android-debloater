namespace AndroidDebloater.Core.Models;

/// <summary>
/// Anzeigegruppe eines Pakets für die Tab-Aufteilung. Sie ergibt sich
/// ausschließlich aus der Herkunft (<see cref="AndroidPackage.Kind"/>) und
/// einem vorhandenen Launcher-Eintrag – es wird nichts geraten.
/// </summary>
public enum PackageCategory
{
    /// <summary>Nachinstallierte App oder Spiel (Herkunft Benutzer).</summary>
    UserInstalled,

    /// <summary>Vorinstallierte App oder Spiel mit Launcher-Eintrag (im App-Drawer sichtbar).</summary>
    SystemApp,

    /// <summary>Systemdienst ohne Launcher-Eintrag (läuft nur im Hintergrund).</summary>
    SystemService,
}
