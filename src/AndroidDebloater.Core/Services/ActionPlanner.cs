using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Bestimmt, welche Aktionen für ein Paket gerade möglich sind (projektinfo.txt
/// M12): Aktivieren/Deaktivieren, Entfernen/Wiederherstellen bei System-Apps sowie
/// das endgültige Entfernen bei nachinstallierten Apps. Geschützte und kritische
/// Pakete (Abschnitt 6.1) bleiben gesperrt; für sie ist die Liste leer.
/// </summary>
public static class ActionPlanner
{
    /// <summary>
    /// Alle Aktionen, die für dieses Paket jetzt erlaubt sind – in der
    /// Reihenfolge, in der sie im Menü erscheinen. Leer bei geschützten oder
    /// kritischen Paketen (S-07/S-20) und wenn nichts zu tun ist.
    /// </summary>
    public static IReadOnlyList<PlannedAction> AvailableActions(AndroidPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (IsBlocked(package))
        {
            return Array.Empty<PlannedAction>();
        }

        var actions = new List<PlannedAction>(3);

        if (!package.Installed)
        {
            // Nur System-Apps lassen sich per ADB wieder einsetzen (cmd package
            // install-existing); eine entfernte Nutzer-App gibt es nicht zurück.
            if (package.Kind == PackageKind.System)
            {
                actions.Add(InstallExistingPlan(package));
            }
            else
            {
                // Nachinstallierte App: Restdaten endgueltig loeschen, damit sie
                // vollstaendig vom Geraet verschwindet (S-21).
                actions.Add(PurgeRemovedDataPlan(package));
            }

            return actions;
        }

        // Deaktivieren/Aktivieren ist bei jeder Herkunft möglich (M12: "beides überall").
        actions.Add(package.IsEnabled ? DisablePlan(package) : EnablePlan(package));

        if (package.Kind == PackageKind.System)
        {
            // System-App: entfernen, aber umkehrbar – Daten bleiben erhalten.
            actions.Add(RemoveSystemPlan(package));
        }
        else
        {
            // Nachinstalliert: endgültig entfernen, wahlweise mit oder ohne Daten (S-21).
            actions.Add(PermanentUninstallPlan(package));
            actions.Add(KeepDataUninstallPlan(package));
        }

        return actions;
    }

    /// <summary>
    /// Ausweichplan, wenn "pm uninstall --user 0 -k" vom System verweigert wird
    /// (projektinfo.txt S-12): Statt zu entfernen wird die System-App für
    /// Benutzer 0 deaktiviert, der Rückweg ist dann "pm enable". Für Nutzer-Apps
    /// gibt es keinen Ausweichplan – dort ist "disable-user" eine eigene Aktion,
    /// und ein endgültiges Entfernen darf nicht heimlich abgeschwächt werden.
    /// </summary>
    public static PlannedAction? CreateFallbackPlan(AndroidPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        if (package.Kind != PackageKind.System || IsBlocked(package))
        {
            return null;
        }

        return DisablePlan(package);
    }

    /// <summary>
    /// Whitelist-Pakete bleiben immer gesperrt. Kritische Pakete (Sperrliste)
    /// sind im Normalbetrieb hart gesperrt (S-07); nur der bewusst
    /// eingeschaltete Experten-Mode öffnet diese Sperre (S-20). Die Prüfung
    /// läuft zusätzlich über die Listen selbst, damit sie auch greift, wenn ein
    /// Paket noch nicht bewertet wurde.
    /// </summary>
    private static bool IsBlocked(AndroidPackage package)
    {
        // Whitelist (P2-5): geschützt – auch im Experten-Mode nicht bearbeitbar.
        if (ProtectionLists.IsWhitelisted(package.PackageName))
        {
            return true;
        }

        // CRITICAL: harte Sperre (S-07), nur der Experten-Mode hebt sie auf (S-20).
        if (package.Safety == PackageSafety.Critical
            || ProtectionLists.IsCritical(package.PackageName))
        {
            return !ProtectionLists.ExpertModeEnabled;
        }

        return package.IsProtected;
    }

    private static PlannedAction RemoveSystemPlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.UninstallForUser,
        Command = UninstallCommand(package.PackageName),
        RestoreAction = PackageAction.InstallExisting,
        RestoreCommand = InstallExistingCommand(package.PackageName),
    };

    private static PlannedAction PermanentUninstallPlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.UninstallPermanent,
        Command = PermanentUninstallCommand(package.PackageName),
        RestoreAction = PackageAction.ReinstallFromStore,
        RestoreCommand = StoreCommand(package.PackageName),
        IsPermanent = true,
    };

    private static PlannedAction KeepDataUninstallPlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.UninstallKeepData,
        Command = UninstallCommand(package.PackageName),
        RestoreAction = PackageAction.ReinstallFromStore,
        RestoreCommand = StoreCommand(package.PackageName),
        IsPermanent = true,
    };

    private static PlannedAction DisablePlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.DisableForUser,
        Command = DisableCommand(package.PackageName),
        RestoreAction = PackageAction.EnableForUser,
        RestoreCommand = EnableCommand(package.PackageName),
    };

    private static PlannedAction EnablePlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.EnableForUser,
        Command = EnableCommand(package.PackageName),
        RestoreAction = PackageAction.DisableForUser,
        RestoreCommand = DisableCommand(package.PackageName),
    };

    private static PlannedAction PurgeRemovedDataPlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.PurgeRemovedData,
        Command = PurgeCommand(package.PackageName),
        RestoreAction = PackageAction.ReinstallFromStore,
        RestoreCommand = StoreCommand(package.PackageName),
        IsPermanent = true,
    };

    private static PlannedAction InstallExistingPlan(AndroidPackage package) => new()
    {
        Package = package,
        Action = PackageAction.InstallExisting,
        Command = InstallExistingCommand(package.PackageName),
        RestoreAction = PackageAction.UninstallForUser,
        RestoreCommand = UninstallCommand(package.PackageName),
    };

    // "-k" zusammen mit "--user 0" ist am TB311FU geprüft: der Datenordner
    // bleibt erhalten, und "install-existing" holt die System-App zurück (B5).
    private static string[] UninstallCommand(string packageName)
        => new[] { "pm", "uninstall", "--user", "0", "-k", packageName };

    // Endgueltiges Entfernen ohne "-k": App UND Daten werden gelöscht (S-21).
    private static string[] PermanentUninstallCommand(string packageName)
        => new[] { "pm", "uninstall", "--user", "0", packageName };

    // Restdaten loeschen: bewusst OHNE "--user 0" und ohne "-k". Am TB311FU
    // geprueft: nur so raeumt "pm uninstall" die mit "-k" behaltenen Daten weg
    // ("Success"); mit "--user 0" kommt "Failure [not installed for 0]".
    private static string[] PurgeCommand(string packageName)
        => new[] { "pm", "uninstall", packageName };

    // Rettungsweg: Play-Store-Seite des Pakets auf dem Gerät öffnen.
    private static string[] StoreCommand(string packageName)
        => new[]
        {
            "am", "start", "-a", "android.intent.action.VIEW",
            "-d", $"market://details?id={packageName}",
        };

    private static string[] InstallExistingCommand(string packageName)
        => new[] { "cmd", "package", "install-existing", "--user", "0", packageName };

    private static string[] DisableCommand(string packageName)
        => new[] { "pm", "disable-user", "--user", "0", packageName };

    private static string[] EnableCommand(string packageName)
        => new[] { "pm", "enable", "--user", "0", packageName };
}
