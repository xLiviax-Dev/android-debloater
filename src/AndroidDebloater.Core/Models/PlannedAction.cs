using AndroidDebloater.Core.Localization;

namespace AndroidDebloater.Core.Models;

/// <summary>
/// Ein fertiger, noch NICHT ausgeführter Aktionsplan für genau ein Paket.
/// Er enthält den Befehl und den passenden Rückweg, damit die UI beides vorher
/// als Dry-Run zeigen kann (Abschnitt 6.3 / S-08). Endgültige Aktionen
/// (<see cref="IsPermanent"/>) haben keinen Rückweg, sondern nur einen
/// Rettungsweg über den Play Store.
/// </summary>
public sealed class PlannedAction
{
    /// <summary>Betroffenes Paket (Paketname und Anzeigename für den Dialog).</summary>
    public required AndroidPackage Package { get; init; }

    /// <summary>Auszuführende Aktion.</summary>
    public required PackageAction Action { get; init; }

    /// <summary>Shell-Befehl als einzelne Argumente, ohne "adb shell" davor.</summary>
    public required IReadOnlyList<string> Command { get; init; }

    /// <summary>Aktion zum Rückgängigmachen.</summary>
    public required PackageAction RestoreAction { get; init; }

    /// <summary>Shell-Befehl des Rückwegs, ebenfalls ohne "adb shell" davor.</summary>
    public required IReadOnlyList<string> RestoreCommand { get; init; }

    /// <summary>
    /// True, wenn die Aktion per ADB nicht zurückgenommen werden kann (S-21).
    /// Für solche Pläne beschreibt <see cref="RestoreCommand"/> nur den
    /// Rettungsweg (Play-Store-Seite öffnen), keinen automatischen Rollback.
    /// </summary>
    public bool IsPermanent { get; init; }

    /// <summary>Beschriftung für Menü und Bestätigungsdialog.</summary>
    public string Label => Action switch
    {
        PackageAction.UninstallForUser => Strings.ActionRemove,
        PackageAction.UninstallPermanent => Strings.ActionUninstallPermanent,
        PackageAction.UninstallKeepData => Strings.ActionUninstallKeepData,
        PackageAction.PurgeRemovedData => Strings.ActionPurgeData,
        PackageAction.DisableForUser => Strings.ActionDisable,
        PackageAction.InstallExisting => Strings.ActionRestore,
        PackageAction.EnableForUser => Strings.ActionEnable,
        _ => Action.ToString(),
    };

    /// <summary>Befehl als lesbarer Text für Vorschau und Protokoll.</summary>
    public string CommandText => string.Join(' ', Command);

    /// <summary>Rückweg als lesbarer Text für Vorschau und Protokoll.</summary>
    public string RestoreCommandText => string.Join(' ', RestoreCommand);
}
