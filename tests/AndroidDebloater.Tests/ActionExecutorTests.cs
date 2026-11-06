using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der zustandsfreien Prüfungen des Ausführers (projektinfo.txt M6.2):
/// Erkennen einer verweigerten Ausführung (S-12) und Vergleich von Aktion und
/// tatsächlichem Paketzustand (S-10). Es wird kein Gerät kontaktiert.
/// </summary>
public class ActionExecutorTests
{
    private static AndroidPackage Create(string name, bool installed, int enabledState)
        => new()
        {
            PackageName = name,
            Installed = installed,
            EnabledState = enabledState,
        };

    [Fact]
    public void IsCommandRefused_NonZeroExitCode_IsRefused()
    {
        var result = new ProcessResult(1, string.Empty, string.Empty);

        Assert.True(ActionExecutor.IsCommandRefused(result));
    }

    [Fact]
    public void IsCommandRefused_FailureOnStdout_IsRefused()
    {
        // So meldet das Testgerät einen verweigerten "pm uninstall"-Aufruf.
        var result = new ProcessResult(0, "Failure [DELETE_FAILED_INTERNAL_ERROR]", string.Empty);

        Assert.True(ActionExecutor.IsCommandRefused(result));
    }

    [Fact]
    public void IsCommandRefused_FailureOnStderr_IsRefused()
    {
        var result = new ProcessResult(0, string.Empty, "Failure [not allowed]");

        Assert.True(ActionExecutor.IsCommandRefused(result));
    }

    [Fact]
    public void IsCommandRefused_Success_IsNotRefused()
    {
        var result = new ProcessResult(0, "Success", string.Empty);

        Assert.False(ActionExecutor.IsCommandRefused(result));
    }

    [Fact]
    public void IsSatisfied_Uninstall_IsTrueWhenRemoved()
    {
        Assert.True(ActionExecutor.IsSatisfied(PackageAction.UninstallForUser, Create("a", false, 0)));
        Assert.False(ActionExecutor.IsSatisfied(PackageAction.UninstallForUser, Create("a", true, 0)));
    }

    [Fact]
    public void IsSatisfied_InstallExisting_IsTrueWhenInstalled()
    {
        Assert.True(ActionExecutor.IsSatisfied(PackageAction.InstallExisting, Create("a", true, 0)));
        Assert.False(ActionExecutor.IsSatisfied(PackageAction.InstallExisting, Create("a", false, 0)));
    }

    [Fact]
    public void IsSatisfied_Disable_IsTrueWhenDisabled()
    {
        Assert.True(ActionExecutor.IsSatisfied(PackageAction.DisableForUser, Create("a", true, 2)));
        Assert.False(ActionExecutor.IsSatisfied(PackageAction.DisableForUser, Create("a", true, 0)));
    }

    [Fact]
    public void IsSatisfied_Enable_IsTrueWhenActive()
    {
        Assert.True(ActionExecutor.IsSatisfied(PackageAction.EnableForUser, Create("a", true, 0)));
        Assert.False(ActionExecutor.IsSatisfied(PackageAction.EnableForUser, Create("a", true, 2)));
    }
}
