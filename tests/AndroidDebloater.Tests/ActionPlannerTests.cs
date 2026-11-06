using System.Linq;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests des Aktionsplaners (projektinfo.txt M6.1/M12): pro Paket alle gerade
/// passenden Aktionen, passende Rückwege, die Sperre für geschützte/kritische
/// Pakete und die endgültigen Entfernungen (S-21).
/// </summary>
public class ActionPlannerTests
{
    /// <summary>Jeder Test startet mit dem Auslieferungszustand (Whitelist, Experten-Mode aus).</summary>
    public ActionPlannerTests() => ProtectionLists.Reset();

    private static AndroidPackage Create(
        string name,
        PackageKind kind = PackageKind.System,
        bool installed = true,
        bool enabled = true,
        PackageSafety safety = PackageSafety.Safe,
        bool isProtected = false)
        => new()
        {
            PackageName = name,
            Kind = kind,
            Installed = installed,
            EnabledState = enabled ? 0 : 2,
            Safety = safety,
            IsProtected = isProtected,
        };

    [Fact]
    public void AvailableActions_SystemApp_OffersDisableAndRemovable()
    {
        var package = Create("com.lenovo.bloatware");

        var actions = ActionPlanner.AvailableActions(package);

        Assert.Equal(
            new[] { PackageAction.DisableForUser, PackageAction.UninstallForUser },
            actions.Select(action => action.Action));

        var disable = actions[0];
        Assert.Equal(
            new[] { "pm", "disable-user", "--user", "0", "com.lenovo.bloatware" },
            disable.Command);
        Assert.Equal(PackageAction.EnableForUser, disable.RestoreAction);
        Assert.Equal(
            new[] { "pm", "enable", "--user", "0", "com.lenovo.bloatware" },
            disable.RestoreCommand);
        Assert.False(disable.IsPermanent);

        var remove = actions[1];
        Assert.Equal(
            new[] { "pm", "uninstall", "--user", "0", "-k", "com.lenovo.bloatware" },
            remove.Command);
        Assert.Equal(PackageAction.InstallExisting, remove.RestoreAction);
        Assert.Equal(
            new[] { "cmd", "package", "install-existing", "--user", "0", "com.lenovo.bloatware" },
            remove.RestoreCommand);
        Assert.False(remove.IsPermanent);
    }

    [Fact]
    public void AvailableActions_UserApp_OffersDisableAndTwoFinalUninstalls()
    {
        var package = Create("com.example.game", PackageKind.User);

        var actions = ActionPlanner.AvailableActions(package);

        Assert.Equal(
            new[]
            {
                PackageAction.DisableForUser,
                PackageAction.UninstallPermanent,
                PackageAction.UninstallKeepData,
            },
            actions.Select(action => action.Action));
    }

    [Fact]
    public void AvailableActions_UserApp_PermanentUninstall_HasNoAdbUndo()
    {
        var package = Create("com.example.game", PackageKind.User);

        var permanent = ActionPlanner.AvailableActions(package)
            .Single(action => action.Action == PackageAction.UninstallPermanent);

        // Ohne "-k": App UND Daten werden gelöscht, kein Rückweg per ADB (S-21).
        Assert.Equal(
            new[] { "pm", "uninstall", "--user", "0", "com.example.game" },
            permanent.Command);
        Assert.True(permanent.IsPermanent);
        Assert.Equal(PackageAction.ReinstallFromStore, permanent.RestoreAction);
        Assert.Equal(
            new[]
            {
                "am", "start", "-a", "android.intent.action.VIEW",
                "-d", "market://details?id=com.example.game",
            },
            permanent.RestoreCommand);
    }

    [Fact]
    public void AvailableActions_UserApp_KeepDataUninstall_KeepsTheDataFolder()
    {
        var package = Create("com.example.game", PackageKind.User);

        var keepData = ActionPlanner.AvailableActions(package)
            .Single(action => action.Action == PackageAction.UninstallKeepData);

        // Mit "-k": der Datenordner bleibt für eine spätere Neuinstallation erhalten.
        Assert.Equal(
            new[] { "pm", "uninstall", "--user", "0", "-k", "com.example.game" },
            keepData.Command);
        Assert.True(keepData.IsPermanent);
        Assert.Equal(PackageAction.ReinstallFromStore, keepData.RestoreAction);
    }

    [Fact]
    public void AvailableActions_RemovedSystemApp_OffersReinstall()
    {
        var package = Create("com.lenovo.bloatware", installed: false);

        var actions = ActionPlanner.AvailableActions(package);

        var restore = Assert.Single(actions);
        Assert.Equal(PackageAction.InstallExisting, restore.Action);
        Assert.Equal(
            new[] { "cmd", "package", "install-existing", "--user", "0", "com.lenovo.bloatware" },
            restore.Command);
    }

    [Fact]
    public void AvailableActions_RemovedUserApp_OffersPurgeData()
    {
        // Eine entfernte Nutzer-App lässt sich per ADB nicht zurückholen, aber
        // ihre behaltenen Restdaten lassen sich endgültig löschen (S-21).
        var package = Create("com.example.game", PackageKind.User, installed: false);

        var purge = Assert.Single(ActionPlanner.AvailableActions(package));
        Assert.Equal(PackageAction.PurgeRemovedData, purge.Action);

        // Am TB311FU geprüft: nur OHNE "--user 0" und ohne "-k" räumt
        // "pm uninstall" die mit "-k" behaltenen Daten weg.
        Assert.Equal(new[] { "pm", "uninstall", "com.example.game" }, purge.Command);
        Assert.True(purge.IsPermanent);
        Assert.Equal(PackageAction.ReinstallFromStore, purge.RestoreAction);
    }

    [Fact]
    public void AvailableActions_DisabledUserApp_OffersEnableFirst()
    {
        var package = Create("com.example.game", PackageKind.User, enabled: false);

        var actions = ActionPlanner.AvailableActions(package);

        Assert.Equal(PackageAction.EnableForUser, actions[0].Action);
        Assert.Equal(
            new[] { "pm", "enable", "--user", "0", "com.example.game" },
            actions[0].Command);
    }

    [Fact]
    public void AvailableActions_CriticalApp_IsEmpty()
    {
        var package = Create("com.android.systemui", safety: PackageSafety.Critical, isProtected: true);

        Assert.Empty(ActionPlanner.AvailableActions(package));
    }

    [Fact]
    public void AvailableActions_WhitelistedApp_IsEmpty()
    {
        var package = Create("com.android.vending", isProtected: true);

        Assert.Empty(ActionPlanner.AvailableActions(package));
    }

    [Fact]
    public void AvailableActions_CriticalApp_IsAllowedInExpertMode()
    {
        // S-20: Der Experten-Mode hebt die harte Sperre (S-07) für CRITICAL auf.
        ProtectionLists.SetExpertMode(true);
        var package = Create("com.android.systemui", safety: PackageSafety.Critical, isProtected: true);

        var actions = ActionPlanner.AvailableActions(package);

        Assert.Contains(actions, action => action.Action == PackageAction.UninstallForUser);
    }

    [Fact]
    public void AvailableActions_WhitelistedApp_StaysBlockedInExpertMode()
    {
        // Die Whitelist bleibt auch im Experten-Mode gesperrt (S-20).
        ProtectionLists.SetExpertMode(true);
        var package = Create("com.android.vending", isProtected: true);

        Assert.Empty(ActionPlanner.AvailableActions(package));
    }

    [Fact]
    public void AvailableActions_CriticalApp_BlockedAgainAfterExpertModeOff()
    {
        // S-20d: Ausschalten stellt die harte Sperre sofort wieder her.
        ProtectionLists.SetExpertMode(true);
        var package = Create("com.android.systemui", safety: PackageSafety.Critical, isProtected: true);
        Assert.NotEmpty(ActionPlanner.AvailableActions(package));

        ProtectionLists.SetExpertMode(false);

        Assert.Empty(ActionPlanner.AvailableActions(package));
    }

    [Fact]
    public void Labels_AreLocalized()
    {
        var system = ActionPlanner.AvailableActions(Create("com.lenovo.bloatware"));
        var user = ActionPlanner.AvailableActions(Create("com.example.game", PackageKind.User));

        Assert.Equal(Strings.ActionDisable, system[0].Label);
        Assert.Equal(Strings.ActionRemove, system[1].Label);
        Assert.Equal(Strings.ActionUninstallPermanent, user[1].Label);
        Assert.Equal(Strings.ActionUninstallKeepData, user[2].Label);
    }

    [Fact]
    public void CreateFallbackPlan_SystemApp_DisablesInstead()
    {
        // Wird "uninstall" verweigert (S-12), bleibt die System-App installiert
        // und wird stattdessen für Benutzer 0 deaktiviert.
        var package = Create("com.lenovo.bloatware");

        var plan = ActionPlanner.CreateFallbackPlan(package);

        Assert.NotNull(plan);
        Assert.Equal(PackageAction.DisableForUser, plan!.Action);
        Assert.Equal(
            new[] { "pm", "disable-user", "--user", "0", "com.lenovo.bloatware" },
            plan.Command);
        Assert.Equal(PackageAction.EnableForUser, plan.RestoreAction);
        Assert.Equal(
            new[] { "pm", "enable", "--user", "0", "com.lenovo.bloatware" },
            plan.RestoreCommand);
    }

    [Fact]
    public void CreateFallbackPlan_UserApp_ReturnsNull()
    {
        // Bei Nutzer-Apps ist "disable-user" bereits eine eigene Aktion – kein Ausweichplan.
        var package = Create("com.example.game", PackageKind.User);

        Assert.Null(ActionPlanner.CreateFallbackPlan(package));
    }

    [Fact]
    public void CreateFallbackPlan_ProtectedApp_ReturnsNull()
    {
        var package = Create("com.android.vending", isProtected: true);

        Assert.Null(ActionPlanner.CreateFallbackPlan(package));
    }

    [Fact]
    public void CommandText_IsReadable()
    {
        var remove = ActionPlanner.AvailableActions(Create("com.lenovo.bloatware"))
            .Single(action => action.Action == PackageAction.UninstallForUser);

        Assert.Equal("pm uninstall --user 0 -k com.lenovo.bloatware", remove.CommandText);
        Assert.Equal("cmd package install-existing --user 0 com.lenovo.bloatware", remove.RestoreCommandText);
    }
}
