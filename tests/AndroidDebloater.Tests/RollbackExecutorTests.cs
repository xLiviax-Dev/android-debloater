using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Ein-Klick-Rollback-Logik (projektinfo.txt M6.5 / S-06):
/// Rücknahme-Reihenfolge, Bewertung des Ergebnisses (S-10/S-19) und die
/// Protokollzeile (S-14). Es wird kein Gerät kontaktiert.
/// </summary>
public class RollbackExecutorTests
{
    private static readonly DateTimeOffset Timestamp =
        new(2026, 11, 2, 12, 0, 0, TimeSpan.FromHours(1));

    private static RollbackEntry Entry(string packageName, PackageAction action)
        => new()
        {
            PackageName = packageName,
            DisplayName = null,
            Action = action,
            WasInstalled = true,
            PreviousEnabledState = 0,
            Command = new[] { "pm", "uninstall", "--user", "0", "-k", packageName },
            RestoreAction = PackageAction.InstallExisting,
            RestoreCommand = new[] { "cmd", "package", "install-existing", "--user", "0", packageName },
            Timestamp = Timestamp,
            Succeeded = true,
        };

    private static AndroidPackage Package(string name, bool installed, int enabledState)
        => new()
        {
            PackageName = name,
            Installed = installed,
            EnabledState = enabledState,
        };

    [Fact]
    public void PlanRestores_ReturnsEntriesInReverseOrder()
    {
        var profile = new RollbackProfile();
        profile.Entries.Add(Entry("com.first", PackageAction.UninstallForUser));
        profile.Entries.Add(Entry("com.second", PackageAction.DisableForUser));
        profile.Entries.Add(Entry("com.third", PackageAction.UninstallForUser));

        var plan = RollbackExecutor.PlanRestores(profile);

        Assert.Equal(new[] { "com.third", "com.second", "com.first" }, plan.Select(e => e.PackageName));
    }

    [Fact]
    public void PlanRestores_EmptyProfile_IsEmpty()
    {
        Assert.Empty(RollbackExecutor.PlanRestores(new RollbackProfile()));
    }

    [Fact]
    public void AssessStatus_RefreshedAndSatisfied_IsSucceeded()
    {
        var result = new ProcessResult(0, "Success", string.Empty);

        var status = RollbackExecutor.AssessStatus(
            result,
            refreshed: true,
            PackageAction.EnableForUser,
            Package("com.app", installed: true, enabledState: 0));

        Assert.Equal(ActionStatus.Succeeded, status);
    }

    [Fact]
    public void AssessStatus_Refused_IsFailed()
    {
        var result = new ProcessResult(0, "Failure [not allowed]", string.Empty);

        var status = RollbackExecutor.AssessStatus(
            result,
            refreshed: true,
            PackageAction.InstallExisting,
            Package("com.app", installed: false, enabledState: 0));

        Assert.Equal(ActionStatus.Failed, status);
    }

    [Fact]
    public void AssessStatus_NotRefreshed_IsUnknown()
    {
        var result = new ProcessResult(0, "Success", string.Empty);

        var status = RollbackExecutor.AssessStatus(
            result,
            refreshed: false,
            PackageAction.EnableForUser,
            Package("com.app", installed: true, enabledState: 2));

        Assert.Equal(ActionStatus.Unknown, status);
    }

    [Fact]
    public void AssessStatus_RefreshedButStateUnchanged_IsUnknown()
    {
        var result = new ProcessResult(0, "Success", string.Empty);

        var status = RollbackExecutor.AssessStatus(
            result,
            refreshed: true,
            PackageAction.EnableForUser,
            Package("com.app", installed: true, enabledState: 2));

        Assert.Equal(ActionStatus.Unknown, status);
    }

    [Fact]
    public void RollbackOutcome_AggregatesCounts()
    {
        var entries = new[]
        {
            new RollbackEntryOutcome("a", null, PackageAction.EnableForUser, "pm enable --user 0 a", ActionStatus.Succeeded, null),
            new RollbackEntryOutcome("b", null, PackageAction.InstallExisting, "cmd package install-existing --user 0 b", ActionStatus.Failed, "boom"),
            new RollbackEntryOutcome("c", null, PackageAction.EnableForUser, "pm enable --user 0 c", ActionStatus.Unknown, null),
        };

        var outcome = new RollbackOutcome("Lenovo TB311FU", Timestamp, entries);

        Assert.Equal(3, outcome.Total);
        Assert.Equal(1, outcome.SucceededCount);
        Assert.Equal(2, outcome.FailedCount);
        Assert.False(outcome.AllSucceeded);
    }

    [Fact]
    public void RollbackOutcome_AllSucceeded_IsTrueWhenEveryEntrySucceeded()
    {
        var entries = new[]
        {
            new RollbackEntryOutcome("a", null, PackageAction.EnableForUser, "pm enable --user 0 a", ActionStatus.Succeeded, null),
        };

        Assert.True(new RollbackOutcome("Lenovo TB311FU", Timestamp, entries).AllSucceeded);
    }

    [Fact]
    public void ActionLogWriter_FormatRollbackEntry_IncludesStatusAndCommand()
    {
        var entry = new RollbackEntryOutcome(
            "com.lenovo.bloatware",
            "Lenovo App",
            PackageAction.InstallExisting,
            "cmd package install-existing --user 0 com.lenovo.bloatware",
            ActionStatus.Succeeded,
            null);

        Assert.Equal(
            "2026-11-02 12:00:00 | Succeeded | com.lenovo.bloatware | rollback/InstallExisting | "
                + "cmd package install-existing --user 0 com.lenovo.bloatware",
            ActionLogWriter.FormatRollbackEntry(Timestamp, entry));
    }

    [Fact]
    public void ActionLogWriter_FormatRollbackEntry_AppendsNote()
    {
        var entry = new RollbackEntryOutcome(
            "com.lenovo.bloatware",
            null,
            PackageAction.EnableForUser,
            "pm enable --user 0 com.lenovo.bloatware",
            ActionStatus.Failed,
            "Command failed: not allowed");

        var line = ActionLogWriter.FormatRollbackEntry(Timestamp, entry);

        Assert.Contains("Failed", line);
        Assert.EndsWith("| Command failed: not allowed", line);
    }
}
