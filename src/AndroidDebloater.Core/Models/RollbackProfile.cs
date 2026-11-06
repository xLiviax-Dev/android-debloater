namespace AndroidDebloater.Core.Models;

/// <summary>
/// Wiederherstellungs-Profil einer Sitzung (projektinfo.txt S-04/S-06):
/// das Zielgerät und alle Änderungen in Reihenfolge der Ausführung. Es wird
/// als JSON gesichert und ist die Grundlage für den Ein-Klick-Rollback.
/// </summary>
public sealed class RollbackProfile
{
    /// <summary>Gerät, gegen das gearbeitet wurde (Modell bzw. Seriennummer).</summary>
    public string? Device { get; set; }

    /// <summary>Zeitpunkt, zu dem das Profil angelegt wurde.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    /// <summary>Alle Einträge in Reihenfolge der Ausführung.</summary>
    public List<RollbackEntry> Entries { get; set; } = new();

    /// <summary>True, wenn noch keine Aktion erfasst wurde.</summary>
    public bool IsEmpty => Entries.Count == 0;
}
