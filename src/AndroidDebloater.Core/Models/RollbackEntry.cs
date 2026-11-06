using System.Text.Json.Serialization;

namespace AndroidDebloater.Core.Models;

/// <summary>
/// Ein einzelner Eintrag im Wiederherstellungs-Profil (projektinfo.txt S-04):
/// Paketname und Zustand VOR der Aktion sowie der ausgeführte Befehl mit seinem
/// Rückweg. Der Eintrag wird vor dem Eingriff angelegt und danach nur noch um
/// Ergebnis und Notiz ergänzt.
/// </summary>
public sealed class RollbackEntry
{
    /// <summary>Betroffenes Paket.</summary>
    public required string PackageName { get; init; }

    /// <summary>Anzeigename zum Zeitpunkt der Aktion (nur zur Lesbarkeit).</summary>
    public string? DisplayName { get; init; }

    /// <summary>Tatsächlich ausgeführte Aktion (nach möglichem Fallback, S-12).</summary>
    public required PackageAction Action { get; set; }

    /// <summary>True, wenn das Paket vor der Aktion installiert war.</summary>
    public required bool WasInstalled { get; init; }

    /// <summary>Enabled-Zustand vor der Aktion (0 = DEFAULT und 1 = ENABLED bedeuten aktiv).</summary>
    public required int PreviousEnabledState { get; init; }

    /// <summary>Ausgeführter Befehl als Argumentliste.</summary>
    public required IReadOnlyList<string> Command { get; set; }

    /// <summary>Aktion zum Rückgängigmachen.</summary>
    public required PackageAction RestoreAction { get; set; }

    /// <summary>Rückweg als Argumentliste.</summary>
    public required IReadOnlyList<string> RestoreCommand { get; set; }

    /// <summary>
    /// True, wenn die Aktion endgültig war (S-21): Sie lässt sich nicht
    /// zurücknehmen, <see cref="RestoreCommand"/> ist nur der Rettungsweg über
    /// den Play Store. Solche Einträge überspringt der Rollback.
    /// </summary>
    public bool IsPermanent { get; init; }

    /// <summary>Zeitpunkt der Aktion.</summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>True, wenn die Aktion (ggf. nach Fallback) als erfolgreich bestätigt wurde.</summary>
    public bool Succeeded { get; set; }

    /// <summary>Kurze Begründung oder Ergebnis (Fallback, Fehler, unbestätigt).</summary>
    public string? Note { get; set; }

    /// <summary>Befehl als lesbarer Text (nicht Teil der JSON-Datei).</summary>
    [JsonIgnore]
    public string CommandText => string.Join(' ', Command);

    /// <summary>Rückweg als lesbarer Text (nicht Teil der JSON-Datei).</summary>
    [JsonIgnore]
    public string RestoreCommandText => string.Join(' ', RestoreCommand);
}
