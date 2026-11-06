using System.Linq;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der M6.2-Ablagen (projektinfo.txt S-04/S-05/S-14):
/// Wiederherstellungs-Profil (JSON), Rettungs-Datei (Klartext) und
/// Aktions-Protokoll. Es wird kein Gerät kontaktiert.
/// </summary>
public class RollbackArtifactsTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 11, 2, 10, 0, 0, 123, TimeSpan.FromHours(1));

    private static AndroidPackage Bloatware()
        => new() { PackageName = "com.lenovo.bloatware", Kind = PackageKind.System };

    /// <summary>Der "Entfernen"-Plan einer System-App aus dem aktuellen Aktionsangebot.</summary>
    private static PlannedAction UninstallPlan()
        => ActionPlanner.AvailableActions(Bloatware())
            .Single(action => action.Action == PackageAction.UninstallForUser);

    private static string TempPath(string fileName)
        => Path.Combine(Path.GetTempPath(), "AndroidDebloater-tests-" + Guid.NewGuid().ToString("N"), fileName);

    [Fact]
    public void RollbackProfileStore_CreateFileName_IsTimestamped()
    {
        Assert.Equal("rollback-20261102-100000-123.json", RollbackProfileStore.CreateFileName(Timestamp));
    }

    [Fact]
    public void RescueFileWriter_CreateFileName_IsTimestamped()
    {
        Assert.Equal("rescue-20261102-100000.txt", RescueFileWriter.CreateFileName(Timestamp));
    }

    [Fact]
    public async Task RollbackProfileStore_SaveAndLoad_RoundTripsProfile()
    {
        var path = TempPath("profile.json");
        var profile = new RollbackProfile { Device = "Lenovo TB311FU", CreatedAt = Timestamp };
        profile.Entries.Add(new RollbackEntry
        {
            PackageName = "com.lenovo.bloatware",
            DisplayName = "Lenovo App",
            Action = PackageAction.UninstallForUser,
            WasInstalled = true,
            PreviousEnabledState = 0,
            Command = new[] { "pm", "uninstall", "--user", "0", "-k", "com.lenovo.bloatware" },
            RestoreAction = PackageAction.InstallExisting,
            RestoreCommand = new[] { "cmd", "package", "install-existing", "--user", "0", "com.lenovo.bloatware" },
            Timestamp = Timestamp,
            Succeeded = true,
            Note = null,
        });

        try
        {
            await RollbackProfileStore.SaveAsync(profile, path);
            var loaded = await RollbackProfileStore.LoadAsync(path);

            Assert.NotNull(loaded);
            Assert.Equal("Lenovo TB311FU", loaded!.Device);
            var entry = Assert.Single(loaded.Entries);
            Assert.Equal("com.lenovo.bloatware", entry.PackageName);
            Assert.Equal(PackageAction.UninstallForUser, entry.Action);
            Assert.Equal(PackageAction.InstallExisting, entry.RestoreAction);
            Assert.True(entry.WasInstalled);
            Assert.True(entry.Succeeded);
            Assert.Equal(
                new[] { "cmd", "package", "install-existing", "--user", "0", "com.lenovo.bloatware" },
                entry.RestoreCommand);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }

    [Fact]
    public async Task RollbackProfileStore_LoadAsync_ReturnsNullWhenMissing()
    {
        var path = TempPath("missing.json");

        Assert.Null(await RollbackProfileStore.LoadAsync(path));
    }

    [Fact]
    public void RescueFileWriter_BuildText_ContainsRestoreCommands()
    {
        var profile = new RollbackProfile { Device = "Lenovo TB311FU", CreatedAt = Timestamp };
        profile.Entries.Add(new RollbackEntry
        {
            PackageName = "com.lenovo.bloatware",
            DisplayName = "Lenovo App",
            Action = PackageAction.UninstallForUser,
            WasInstalled = true,
            PreviousEnabledState = 0,
            Command = new[] { "pm", "uninstall", "--user", "0", "-k", "com.lenovo.bloatware" },
            RestoreAction = PackageAction.InstallExisting,
            RestoreCommand = new[] { "cmd", "package", "install-existing", "--user", "0", "com.lenovo.bloatware" },
            Timestamp = Timestamp,
        });

        var text = RescueFileWriter.BuildText(profile);

        Assert.Contains("Android Debloater - rescue file", text);
        Assert.Contains("Device: Lenovo TB311FU", text);
        Assert.Contains("# was done: adb shell pm uninstall --user 0 -k com.lenovo.bloatware", text);
        Assert.Contains("adb shell cmd package install-existing --user 0 com.lenovo.bloatware", text);
    }

    [Fact]
    public void RescueFileWriter_BuildText_EmptyProfile_NotesNoActions()
    {
        var profile = new RollbackProfile();

        var text = RescueFileWriter.BuildText(profile);

        Assert.Contains("(no actions recorded)", text);
    }

    [Fact]
    public void ActionLogWriter_FormatEntry_IncludesBeforeAndAfter()
    {
        var plan = UninstallPlan();
        var outcome = new ActionOutcome
        {
            Plan = plan,
            Status = ActionStatus.Succeeded,
            EffectiveAction = PackageAction.UninstallForUser,
            WasInstalled = true,
            PreviousEnabledState = 0,
            IsInstalled = false,
            CurrentEnabledState = 0,
            Timestamp = Timestamp,
            ExecutedCommands = new[] { plan.CommandText },
        };

        Assert.Equal(
            "2026-11-02 10:00:00 | Succeeded | com.lenovo.bloatware | UninstallForUser | "
                + "installed/active -> removed | pm uninstall --user 0 -k com.lenovo.bloatware",
            ActionLogWriter.FormatEntry(outcome));
    }

    [Fact]
    public void ActionLogWriter_FormatEntry_AppendsNote()
    {
        var plan = UninstallPlan();
        var outcome = new ActionOutcome
        {
            Plan = plan,
            Status = ActionStatus.FallbackSucceeded,
            EffectiveAction = PackageAction.DisableForUser,
            WasInstalled = true,
            PreviousEnabledState = 0,
            IsInstalled = true,
            CurrentEnabledState = 2,
            Timestamp = Timestamp,
            ExecutedCommands = new[] { plan.CommandText, "pm disable-user --user 0 com.lenovo.bloatware" },
            Message = "fallback",
        };

        var line = ActionLogWriter.FormatEntry(outcome);

        Assert.Contains("FallbackSucceeded", line);
        Assert.Contains("installed/active -> installed/disabled(2)", line);
        Assert.Contains("pm disable-user --user 0 com.lenovo.bloatware", line);
        Assert.EndsWith("| fallback", line);
    }

    [Fact]
    public async Task ActionLogWriter_AppendAsync_AppendsEveryEntry()
    {
        var path = TempPath("actions.log");
        var plan = UninstallPlan();
        var outcome = new ActionOutcome
        {
            Plan = plan,
            Status = ActionStatus.Succeeded,
            EffectiveAction = PackageAction.UninstallForUser,
            WasInstalled = true,
            PreviousEnabledState = 0,
            IsInstalled = false,
            CurrentEnabledState = 0,
            Timestamp = Timestamp,
            ExecutedCommands = new[] { plan.CommandText },
        };

        try
        {
            await ActionLogWriter.AppendAsync(outcome, path);
            await ActionLogWriter.AppendAsync(outcome, path);

            var lines = await File.ReadAllLinesAsync(path);
            Assert.Equal(2, lines.Length);
            Assert.Equal(ActionLogWriter.FormatEntry(outcome), lines[0]);
            Assert.Equal(lines[0], lines[1]);
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(path)!, recursive: true);
        }
    }
}
