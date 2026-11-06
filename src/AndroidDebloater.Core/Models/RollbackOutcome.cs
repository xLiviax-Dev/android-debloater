namespace AndroidDebloater.Core.Models;

/// <summary>Ergebnis einer einzelnen Rücknahme beim Ein-Klick-Rollback (S-06).</summary>
/// <param name="PackageName">Betroffenes Paket.</param>
/// <param name="DisplayName">Anzeigename zum Zeitpunkt der Aktion (nur zur Lesbarkeit).</param>
/// <param name="RestoreAction">Ausgeführte Rücknahme-Aktion.</param>
/// <param name="RestoreCommandText">Rückweg als lesbarer Befehl.</param>
/// <param name="Status">Bestätigter Zustand nach der Rücknahme (S-10).</param>
/// <param name="Note">Kurze Begründung oder Ergebnis; null bei glattem Erfolg.</param>
public sealed record RollbackEntryOutcome(
    string PackageName,
    string? DisplayName,
    PackageAction RestoreAction,
    string RestoreCommandText,
    ActionStatus Status,
    string? Note);

/// <summary>
/// Ergebnis eines Ein-Klick-Rollbacks über ein ganzes Profil (S-06): je Eintrag
/// das bestätigte Ergebnis und in Summe, wie viele Änderungen zurückgenommen
/// werden konnten.
/// </summary>
/// <param name="Device">Gerät, gegen das gearbeitet wurde.</param>
/// <param name="Timestamp">Zeitpunkt der Rücknahme.</param>
/// <param name="Entries">Ergebnisse in Reihenfolge der Ausführung.</param>
public sealed record RollbackOutcome(
    string? Device,
    DateTimeOffset Timestamp,
    IReadOnlyList<RollbackEntryOutcome> Entries)
{
    /// <summary>Anzahl der versuchten Rücknahmen.</summary>
    public int Total => Entries.Count;

    /// <summary>Anzahl der bestätigt wiederhergestellten Pakete.</summary>
    public int SucceededCount => Entries.Count(entry => entry.Status == ActionStatus.Succeeded);

    /// <summary>Anzahl der endgültigen Änderungen, die es nicht zurückzunehmen gibt (S-21).</summary>
    public int NotRestorableCount => Entries.Count(entry => entry.Status == ActionStatus.NotRestorable);

    /// <summary>Anzahl der versuchten, aber nicht bestätigten oder fehlgeschlagenen Rücknahmen.</summary>
    public int FailedCount => Total - SucceededCount - NotRestorableCount;

    /// <summary>True, wenn alle versuchten Rücknahmen bestätigt wurden und nichts endgültig war.</summary>
    public bool AllSucceeded => Total > 0 && FailedCount == 0 && NotRestorableCount == 0;
}
