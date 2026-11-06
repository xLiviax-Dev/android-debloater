using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der aktionsbezogenen Zustandswerte eines Pakets (projektinfo.txt
/// M6.3/M12): Freigabe des Aktionsmenüs je Zustand und die Benachrichtigung der
/// Oberfläche nach einer Zustandsänderung.
/// </summary>
public class AndroidPackageActionStateTests
{
    public AndroidPackageActionStateTests() => ProtectionLists.Reset();

    private static AndroidPackage Create(
        string name,
        PackageKind kind,
        bool installed = true,
        bool enabled = true)
        => new()
        {
            PackageName = name,
            Kind = kind,
            Installed = installed,
            EnabledState = enabled ? 0 : 2,
        };

    [Fact]
    public void SystemApp_Installed_HasActions()
    {
        var package = Create("com.lenovo.bloatware", PackageKind.System);

        Assert.True(package.HasActions);
    }

    [Fact]
    public void SystemApp_Removed_StillHasActions()
    {
        // Eine entfernte System-App lässt sich per "install-existing" zurückholen.
        var package = Create("com.lenovo.bloatware", PackageKind.System, installed: false);

        Assert.True(package.HasActions);
    }

    [Fact]
    public void UserApp_Enabled_HasActions()
    {
        var package = Create("com.example.game", PackageKind.User);

        Assert.True(package.HasActions);
    }

    [Fact]
    public void UserApp_Disabled_StillHasActions()
    {
        var package = Create("com.example.game", PackageKind.User, enabled: false);

        Assert.True(package.HasActions);
    }

    [Fact]
    public void UserApp_Removed_StillHasActions()
    {
        // Eine entfernte Nutzer-App lässt sich nicht zurückholen, bietet aber
        // das endgültige Löschen der Restdaten an (S-21).
        var package = Create("com.example.game", PackageKind.User, installed: false);

        Assert.True(package.HasActions);
    }

    [Fact]
    public void WhitelistedApp_HasNoActions()
    {
        var package = Create("com.android.vending", PackageKind.System);

        Assert.False(package.HasActions);
    }

    [Fact]
    public void EnabledStateChange_NotifiesActionStateProperties()
    {
        var package = Create("com.example.game", PackageKind.User);
        var changed = new List<string?>();
        package.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        package.EnabledState = 2;

        Assert.Contains(nameof(AndroidPackage.StatusText), changed);
        Assert.Contains(nameof(AndroidPackage.IsEnabled), changed);
        Assert.Contains(nameof(AndroidPackage.HasActions), changed);
    }

    [Fact]
    public void InstalledChange_NotifiesActionStateProperties()
    {
        var package = Create("com.lenovo.bloatware", PackageKind.System);
        var changed = new List<string?>();
        package.PropertyChanged += (_, args) => changed.Add(args.PropertyName);

        package.Installed = false;

        Assert.Contains(nameof(AndroidPackage.HasActions), changed);
    }
}
