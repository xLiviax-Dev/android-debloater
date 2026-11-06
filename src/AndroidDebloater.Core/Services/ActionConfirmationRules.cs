using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Regeln für den Bestätigungsdialog einer Aktion (projektinfo.txt M6.4/M12):
/// wann erst der abgetippte Paketname die Aktion freigibt (S-09/S-16) und
/// welche Hinweise vorher anzuzeigen sind (R-07 Speicher, R-08 Rollback).
/// </summary>
public static class ActionConfirmationRules
{
    /// <summary>
    /// True, wenn die Aktion nur nach Abtippen des Paketnamens laufen darf:
    /// CAUTION nach S-09/S-16 und CRITICAL nach S-20c (CRITICAL ist ohnehin nur
    /// im Experten-Mode erreichbar). SAFE-Pakete (normale Nutzer-Apps/-Spiele)
    /// brauchen das NICHT – dort genügt der Klick im Bestätigungsdialog, auch
    /// bei einer endgültigen Aktion (Nutzerentscheidung 2026-11-03, S-21).
    /// </summary>
    public static bool RequiresTyping(PackageSafety safety)
        => safety != PackageSafety.Safe;

    /// <summary>
    /// Warnhinweise vor der Aktion, in Anzeige-Reihenfolge: zuerst der
    /// Aktions-Hinweis (R-07 Speicher, R-08 Rollback bzw. S-21 endgültig),
    /// dann die Gefahren-Hürde (CAUTION nach S-16 bzw. CRITICAL nach S-20).
    /// Leer, wenn nichts zutrifft (z. B. beim Rückweg).
    /// </summary>
    public static IReadOnlyList<string> Warnings(PackageAction action, PackageSafety safety)
    {
        var lines = new List<string>();
        switch (action)
        {
            case PackageAction.UninstallForUser:
                lines.Add(Strings.ActionConfirmStorageWarning);
                break;
            case PackageAction.UninstallPermanent:
            case PackageAction.UninstallKeepData:
                lines.Add(Strings.ActionConfirmPermanentWarning);
                break;
            case PackageAction.PurgeRemovedData:
                lines.Add(Strings.ActionConfirmPurgeWarning);
                break;
            case PackageAction.DisableForUser:
                lines.Add(Strings.ActionConfirmPlayWarning);
                break;
        }

        if (safety == PackageSafety.Critical)
        {
            lines.Add(Strings.ActionConfirmExpertWarning);
        }
        else if (safety != PackageSafety.Safe)
        {
            lines.Add(Strings.ActionConfirmCautionWarning);
        }

        return lines;
    }
}
