using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Schutzlisten (projektinfo.txt Abschnitt 6.1): feste Paketnamen,
/// KEINE Präfix-Muster – deshalb darf z. B. com.android.chrome nicht gesperrt sein.
/// </summary>
public class ProtectionListsTests
{
    /// <summary>Jeder Test startet mit dem Auslieferungszustand (Whitelist, Experten-Mode aus).</summary>
    public ProtectionListsTests() => ProtectionLists.Reset();

    [Theory]
    [InlineData("com.android.vending")]
    [InlineData("com.google.android.gms")]
    [InlineData("com.google.android.gms.supervision")]
    public void DefaultWhitelist_ContainsStandardSet(string packageName)
    {
        Assert.True(ProtectionLists.IsWhitelisted(packageName));
        Assert.Contains(packageName, ProtectionLists.DefaultWhitelist);
    }

    [Theory]
    [InlineData("com.example.android.factoryreset")]
    [InlineData("com.android.systemui")]
    [InlineData("com.android.phone")]
    [InlineData("com.android.providers.telephony")]
    [InlineData("com.android.server.telecom")]
    [InlineData("com.android.packageinstaller")]
    [InlineData("com.google.android.packageinstaller")]
    [InlineData("com.android.settings")]
    [InlineData("com.android.providers.settings")]
    public void Critical_ContainsLockedPackages(string packageName)
    {
        Assert.True(ProtectionLists.IsCritical(packageName));
    }

    [Theory]
    [InlineData("com.android.chrome")]
    [InlineData("com.android.fmradio")]
    [InlineData("com.google.android.apps.docs")]
    [InlineData("com.zhiliaoapp.musically")]
    public void Critical_DoesNotLockOrdinaryPackages(string packageName)
    {
        Assert.False(ProtectionLists.IsCritical(packageName));
    }

    [Fact]
    public void FamilyLinkAgent_IsWhitelistedButNotCritical()
    {
        // Der Family-Link-Agent ist eine normale Google-Komponente: er gehört
        // auf die (editierbare) Whitelist, nicht auf die harte Sperrliste.
        const string supervision = "com.google.android.gms.supervision";

        Assert.True(ProtectionLists.IsWhitelisted(supervision));
        Assert.False(ProtectionLists.IsCritical(supervision));
    }

    [Fact]
    public void SetWhitelist_ReplacesEntriesAndTrimsDuplicates()
    {
        ProtectionLists.SetWhitelist(new[] { " com.example.keep ", "com.example.keep", "" });

        Assert.Equal(new[] { "com.example.keep" }, ProtectionLists.Whitelist);
        Assert.True(ProtectionLists.IsWhitelisted("com.example.keep"));
        Assert.False(ProtectionLists.IsWhitelisted("com.android.vending"));
    }

    [Fact]
    public void SetExpertMode_DefaultsToOffAndIsToggleable()
    {
        Assert.False(ProtectionLists.ExpertModeEnabled);

        ProtectionLists.SetExpertMode(true);
        Assert.True(ProtectionLists.ExpertModeEnabled);

        ProtectionLists.SetExpertMode(false);
        Assert.False(ProtectionLists.ExpertModeEnabled);
    }

    [Fact]
    public void Reset_RestoresWhitelistAndExpertMode()
    {
        ProtectionLists.SetWhitelist(new[] { "com.example.only" });
        ProtectionLists.SetExpertMode(true);

        ProtectionLists.Reset();

        Assert.True(ProtectionLists.IsWhitelisted("com.android.vending"));
        Assert.False(ProtectionLists.IsWhitelisted("com.example.only"));
        Assert.False(ProtectionLists.ExpertModeEnabled);
    }
}
