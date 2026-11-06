namespace AndroidDebloater.Core.Models;

/// <summary>
/// Ergebnis einer ausgeführten Aktion (projektinfo.txt S-10/S-12/S-19).
/// </summary>
public enum ActionStatus
{
    /// <summary>Der neue Zustand wurde wie erwartet bestätigt.</summary>
    Succeeded,

    /// <summary>"uninstall" wurde verweigert und "disable-user" hat übernommen (S-12).</summary>
    FallbackSucceeded,

    /// <summary>Der Befehl ist fehlgeschlagen.</summary>
    Failed,

    /// <summary>Der Zustand ließ sich nicht bestätigen (z. B. Verbindung weg, S-19).</summary>
    Unknown,

    /// <summary>Endgültige Aktion (S-21): Es gibt keinen Rückweg, nur den Play Store.</summary>
    NotRestorable,
}

/// <summary>
/// Ergebnis einer ausgeführten Aktion samt Vorher-/Nachher-Zustand – die
/// Grundlage für Protokoll (S-14) und Anzeige.
/// </summary>
public sealed class ActionOutcome
{
    /// <summary>Der ausgeführte Plan.</summary>
    public required PlannedAction Plan { get; init; }

    /// <summary>Ergebnis der Aktion.</summary>
    public required ActionStatus Status { get; init; }

    /// <summary>Tatsächlich wirksame Aktion (nach möglichem Fallback, S-12).</summary>
    public required PackageAction EffectiveAction { get; init; }

    /// <summary>True, wenn das Paket vor der Aktion installiert war.</summary>
    public required bool WasInstalled { get; init; }

    /// <summary>Enabled-Zustand vor der Aktion (0 = aktiv).</summary>
    public required int PreviousEnabledState { get; init; }

    /// <summary>True, wenn das Paket nach der Aktion installiert ist.</summary>
    public required bool IsInstalled { get; init; }

    /// <summary>Enabled-Zustand nach der Aktion (0 = aktiv).</summary>
    public required int CurrentEnabledState { get; init; }

    /// <summary>Zeitpunkt der Ausführung.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>Ausgeführte Befehle als lesbarer Text, in Reihenfolge (inkl. Fallback).</summary>
    public required IReadOnlyList<string> ExecutedCommands { get; init; }

    /// <summary>Kurze Begründung oder Ergebnis; null bei glattem Erfolg.</summary>
    public string? Message { get; init; }

    /// <summary>Rohausgabe des letzten Befehls (falls vorhanden).</summary>
    public string? CommandOutput { get; init; }

    /// <summary>Pfad des gesicherten Wiederherstellungs-Profils (S-04).</summary>
    public string? RollbackProfilePath { get; init; }

    /// <summary>True, wenn die Aktion (ggf. nach Fallback) erfolgreich war.</summary>
    public bool Succeeded => Status is ActionStatus.Succeeded or ActionStatus.FallbackSucceeded;

    /// <summary>True, wenn sich der Zustand des Pakets tatsächlich geändert hat.</summary>
    public bool ChangedState => IsInstalled != WasInstalled || CurrentEnabledState != PreviousEnabledState;
}
