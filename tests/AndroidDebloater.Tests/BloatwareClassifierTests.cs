using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Bloatware-Bewertung und der Gefahren-Einstufung
/// (projektinfo.txt Abschnitt 6). Die Signale sind aus dem Prototyp
/// übernommen und hier gegen feste Erwartungen abgesichert.
/// </summary>
public class BloatwareClassifierTests
{
    /// <summary>Jeder Test startet mit dem Auslieferungszustand (Whitelist, Experten-Mode aus).</summary>
    public BloatwareClassifierTests() => ProtectionLists.Reset();

    private static AndroidPackage Create(
        string name,
        PackageKind kind = PackageKind.System,
        string? installer = null)
        => new()
        {
            PackageName = name,
            Kind = kind,
            InstallerPackage = installer,
        };

    [Fact]
    public void Assess_CriticalPackage_IsLockedAndProtected()
    {
        var package = Create("com.android.systemui");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Critical, package.Safety);
        Assert.True(package.IsLocked);
        Assert.True(package.IsProtected);
    }

    [Fact]
    public void Assess_CriticalPackage_HasReasonAndNoScore()
    {
        var package = Create("com.example.android.factoryreset");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Critical, package.Safety);
        Assert.Equal(0, package.BloatScore);
        Assert.False(package.IsBloatCandidate);
        Assert.NotEmpty(package.BloatReasons);
    }

    [Fact]
    public void Assess_WhitelistedPackage_IsProtectedButNotLocked()
    {
        var package = Create("com.android.vending");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Safe, package.Safety);
        Assert.True(package.IsProtected);
        Assert.False(package.IsLocked);
        Assert.Equal(0, package.BloatScore);
    }

    [Fact]
    public void Assess_UserInstalledApp_IsSafeWithoutScore()
    {
        var package = Create("com.example.mygame", PackageKind.User, "com.android.vending");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Safe, package.Safety);
        Assert.Equal(0, package.BloatScore);
        Assert.False(package.IsBloatCandidate);
        Assert.NotEmpty(package.BloatReasons);
    }

    [Fact]
    public void Assess_UserInstalledAppWithoutInstaller_IsSafe()
    {
        var package = Create("org.example.sideload", PackageKind.User);

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Safe, package.Safety);
        Assert.Equal(0, package.BloatScore);
    }

    [Fact]
    public void Assess_SystemImageAppWithoutSignals_IsCautionNotCandidate()
    {
        var package = Create("com.google.android.apps.docs");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Caution, package.Safety);
        Assert.False(package.IsBloatCandidate);
        Assert.Equal(0, package.BloatScore);
    }

    [Fact]
    public void Assess_PreinstalledThirdParty_IsCandidateAndSafe()
    {
        var package = Create("com.example.addon");

        BloatwareClassifier.Assess(package);

        Assert.True(package.IsBloatCandidate);
        Assert.Equal(50, package.BloatScore);
        Assert.Equal(PackageSafety.Safe, package.Safety);
    }

    [Fact]
    public void Assess_StrongNamePattern_AddsToScore()
    {
        var package = Create("com.vendor.whatsnew");

        BloatwareClassifier.Assess(package);

        Assert.Equal(100, package.BloatScore);
        Assert.True(package.IsBloatCandidate);
        Assert.Contains(package.BloatReasons, reason => reason.Contains("whatsnew"));
    }

    [Fact]
    public void Assess_WeakNamePattern_ReachesThreshold()
    {
        var package = Create("com.example.feedback");

        BloatwareClassifier.Assess(package);

        Assert.Equal(70, package.BloatScore);
        Assert.True(package.IsBloatCandidate);
    }

    [Fact]
    public void Assess_KnownPromo_IsCandidateWithHighScore()
    {
        var package = Create("com.einnovation.temu");

        BloatwareClassifier.Assess(package);

        Assert.True(package.IsBloatCandidate);
        Assert.True(package.BloatScore >= 60);
    }

    [Fact]
    public void Assess_ScoreIsClampedTo100()
    {
        var package = Create("com.einnovation.temu.whatsnew.feedback");

        BloatwareClassifier.Assess(package);

        Assert.Equal(100, package.BloatScore);
    }

    [Fact]
    public void Assess_FrameworkPackage_IsCautionAndNotCandidate()
    {
        var package = Create("android");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Caution, package.Safety);
        Assert.Equal(0, package.BloatScore);
        Assert.False(package.IsBloatCandidate);
    }

    [Fact]
    public void Assess_OverlayPackage_IsCaution()
    {
        var package = Create("com.android.internal.display.cutout.emulation.hole");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Caution, package.Safety);
        Assert.False(package.IsBloatCandidate);
    }

    [Fact]
    public void Assess_VendorAppWithBloatPattern_IsCandidate()
    {
        var package = Create("com.lenovo.screensaver");

        BloatwareClassifier.Assess(package);

        Assert.True(package.IsBloatCandidate);
        Assert.Equal(50, package.BloatScore);
    }

    [Fact]
    public void Assess_CustomWhitelistEntry_IsProtectedAndSafe()
    {
        // Ein vom Nutzer ergänzter Whitelist-Eintrag schützt das Paket (M8 / P2-5).
        ProtectionLists.SetWhitelist(new[] { "com.lenovo.customtool" });
        var package = Create("com.lenovo.customtool");

        BloatwareClassifier.Assess(package);

        Assert.Equal(PackageSafety.Safe, package.Safety);
        Assert.True(package.IsProtected);
        Assert.Equal(0, package.BloatScore);
    }
}
