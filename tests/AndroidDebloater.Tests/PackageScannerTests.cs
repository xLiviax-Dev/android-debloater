using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests des Paket-Scanners gegen die am Testgerät (Lenovo TB311FU, Android 16)
/// verifizierten Ausgabeformate von "pm list packages -f" und "dumpsys package".
/// </summary>
public class PackageScannerTests
{
    /// <summary>
    /// Echter Eintrag des Testgeräts. Der Code-Pfad enthält "==" (base64-Ordner
    /// unter /data/app), weshalb am letzten "=" getrennt werden muss.
    /// </summary>
    private const string WellbeingLine =
        "package:/data/app/~~sbNh2HzBxXmI_aQ5eld-IQ==/com.google.android.apps.wellbeing-adWEWT8a_8qiGfsIo9Dwpw==/base.apk=com.google.android.apps.wellbeing";

    private const string FmRadioLine =
        "package:/system_ext/priv-app/FMRadio/FMRadio.apk=com.android.fmradio";

    /// <summary>Echter Paketblock von com.android.fmradio (gekürzt auf relevante Zeilen).</summary>
    private const string FmRadioBlock = """
          Package [com.android.fmradio] (784765f):
            appId=10184
            codePath=/system_ext/priv-app/FMRadio
            resourcePath=/system_ext/priv-app/FMRadio
            versionName=2.0
            flags=[ SYSTEM HAS_CODE ALLOW_CLEAR_USER_DATA ALLOW_BACKUP ]
            privateFlags=[ PRIVATE_FLAG_ACTIVITIES_RESIZE_MODE_RESIZEABLE_VIA_SDK_VERSION ALLOW_AUDIO_PLAYBACK_CAPTURE PRIVILEGED SYSTEM_EXT ]
            installerPackageName=null
            lastUpdateTime=2025-06-13 06:24:57
            User 0: ceDataInode=3421 deDataInode=1395 installed=true hidden=false suspended=false distractionFlags=0 stopped=true notLaunched=false enabled=0 instant=false virtual=false quarantined=false
              firstInstallTime=2025-06-13 06:24:57
        """;

    [Fact]
    public void ParsePackagePaths_SplitsAtLastEquals_BecausePathContainsDoubleEquals()
    {
        var paths = PackageScanner.ParsePackagePaths(WellbeingLine);

        Assert.Equal(
            "/data/app/~~sbNh2HzBxXmI_aQ5eld-IQ==/com.google.android.apps.wellbeing-adWEWT8a_8qiGfsIo9Dwpw==/base.apk",
            paths["com.google.android.apps.wellbeing"]);
    }

    [Fact]
    public void ParsePackagePaths_ReadsSimplePath()
    {
        var paths = PackageScanner.ParsePackagePaths(FmRadioLine);

        Assert.Equal("/system_ext/priv-app/FMRadio/FMRadio.apk", paths["com.android.fmradio"]);
    }

    [Fact]
    public void ParsePackagePaths_IgnoresNoise()
    {
        var paths = PackageScanner.ParsePackagePaths(
            "List of devices attached\npackage:=com.broken\npackage:/system/app/Foo.apk=\n");

        Assert.Empty(paths);
    }

    [Fact]
    public void ParseDump_ReadsSystemPackageWithPrivilegedFlag()
    {
        var packages = PackageScanner.ParseDump(FmRadioBlock);

        var package = Assert.Single(packages);
        Assert.Equal("com.android.fmradio", package.PackageName);
        Assert.Equal(PackageKind.System, package.Kind);
        Assert.True(package.IsPrivilegedApp);
        Assert.Equal("/system_ext/priv-app/FMRadio", package.CodePath);
        Assert.Equal("2.0", package.VersionName);
        Assert.True(package.Installed);
        Assert.False(package.Hidden);
        Assert.False(package.Suspended);
        Assert.True(package.Stopped);
        Assert.Equal(0, package.EnabledState);
        Assert.True(package.IsEnabled);
    }

    [Fact]
    public void ParseDump_TreatsLiteralNullInstallerAsUnknown()
    {
        var packages = PackageScanner.ParseDump(FmRadioBlock);

        var package = Assert.Single(packages);
        Assert.Null(package.InstallerPackage);
        Assert.Equal(Strings.ValueUnknown, package.InstallerText);
    }

    [Fact]
    public void ParseDump_ReadsInstallDate()
    {
        var packages = PackageScanner.ParseDump(FmRadioBlock);

        var package = Assert.Single(packages);
        Assert.NotNull(package.FirstInstallTime);
        Assert.Equal(new DateTime(2025, 6, 13, 6, 24, 57), package.FirstInstallTime!.Value.DateTime);
    }

    [Fact]
    public void ParseDump_Deduplicates_AndPrefersActiveDataAppVersion()
    {
        const string dump = """
              Package [com.android.chrome] (aaa):
                codePath=/product/app/Chrome
                versionName=100.0
                flags=[ SYSTEM HAS_CODE ]
                installerPackageName=null
                User 0: installed=true hidden=false suspended=false stopped=false enabled=0
                  firstInstallTime=2025-01-01 00:00:00
              Package [com.android.chrome] (bbb):
                codePath=/data/app/~~abc==/com.android.chrome-def==
                versionName=120.0
                flags=[ SYSTEM UPDATED_SYSTEM_APP HAS_CODE ]
                installerPackageName=com.android.vending
                User 0: installed=true hidden=true suspended=false stopped=false enabled=0
                  firstInstallTime=2025-02-02 00:00:00
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.Equal(PackageKind.System, package.Kind);
        Assert.True(package.IsUpdatedSystemApp);
        Assert.Equal("120.0", package.VersionName);
        Assert.Equal("com.android.vending", package.InstallerPackage);
        Assert.True(package.Hidden);
    }

    [Fact]
    public void ParseDump_Deduplicates_RegardlessOfOrder()
    {
        const string dump = """
              Package [com.android.chrome] (bbb):
                codePath=/data/app/~~abc==/com.android.chrome-def==
                flags=[ SYSTEM UPDATED_SYSTEM_APP HAS_CODE ]
                User 0: installed=true hidden=false suspended=false stopped=false enabled=0
              Package [com.android.chrome] (aaa):
                codePath=/product/app/Chrome
                flags=[ SYSTEM HAS_CODE ]
                User 0: installed=true hidden=false suspended=false stopped=false enabled=0
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.Equal("/data/app/~~abc==/com.android.chrome-def==", package.CodePath);
    }

    [Fact]
    public void ParseDump_ClassifiesUserAppAndDisabledState()
    {
        const string dump = """
              Package [com.example.game] (ccc):
                codePath=/data/app/~~xyz==/com.example.game-1==
                flags=[ HAS_CODE ALLOW_BACKUP ]
                installerPackageName=com.android.vending
                User 0: installed=true hidden=false suspended=false stopped=false enabled=3
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.Equal(PackageKind.User, package.Kind);
        Assert.False(package.IsEnabled);
        Assert.Equal(Strings.StatusDisabled, package.StatusText);
        Assert.Equal(Strings.KindUser, package.KindText);
    }

    [Fact]
    public void ParseDump_TreatsEnabledStateOneAsActive()
    {
        // Am TB311FU geprüft: "pm enable" setzt den Zustand auf 1 (ENABLED);
        // das Paket steht dann in "pm list packages -e", nicht in "-d".
        const string dump = """
              Package [com.example.game] (ccc):
                codePath=/data/app/~~xyz==/com.example.game-1==
                flags=[ HAS_CODE ALLOW_BACKUP ]
                installerPackageName=com.android.vending
                User 0: installed=true hidden=false suspended=false stopped=false enabled=1
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.Equal(1, package.EnabledState);
        Assert.True(package.IsEnabled);
        Assert.Equal(Strings.StatusActive, package.StatusText);
    }

    [Fact]
    public void ParseDump_ReportsRemovedPackage()
    {
        const string dump = """
              Package [com.example.removed] (ddd):
                codePath=/data/app/~~xyz==/com.example.removed-1==
                flags=[ HAS_CODE ]
                User 0: installed=false hidden=true suspended=false stopped=true enabled=1
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.False(package.Installed);
        Assert.Equal(Strings.StatusRemoved, package.StatusText);
    }

    [Fact]
    public void ParseDump_DerivesKindFromCodePath_WhenFlagsMissing()
    {
        const string dump = """
              Package [com.example.overlay] (eee):
                codePath=/product/overlay/ExampleOverlay.apk
            """;

        var packages = PackageScanner.ParseDump(dump);

        var package = Assert.Single(packages);
        Assert.Equal(PackageKind.System, package.Kind);
    }

    [Fact]
    public void ParseDump_UsesPathFallback_WhenCodePathMissing()
    {
        const string dump = """
              Package [com.android.fmradio] (784765f):
                versionName=2.0
                flags=[ SYSTEM HAS_CODE ]
                User 0: installed=true hidden=false suspended=false stopped=false enabled=0
            """;

        var fallback = PackageScanner.ParsePackagePaths(FmRadioLine);
        var packages = PackageScanner.ParseDump(dump, fallback);

        var package = Assert.Single(packages);
        Assert.Equal("/system_ext/priv-app/FMRadio/FMRadio.apk", package.CodePath);
    }

    [Fact]
    public void SizeText_SumsApkDataAndCache()
    {
        var package = new AndroidPackage
        {
            PackageName = "com.example.app",
            ApkSizeBytes = 1024 * 1024,
            DataSizeBytes = 1024 * 1024,
            CacheSizeBytes = 0,
        };

        Assert.Equal(2 * 1024 * 1024, package.TotalSizeBytes);
        Assert.Equal(Humanize.Bytes(2 * 1024 * 1024), package.SizeText);
    }

    [Fact]
    public void SizeText_IsUnknown_WhenNoSizes()
    {
        var package = new AndroidPackage { PackageName = "com.example.app" };

        Assert.Equal(Strings.ValueUnknown, package.SizeText);
        Assert.Equal(Strings.ValueUnknown, package.VersionText);
        Assert.Equal(Strings.ValueUnknown, package.InstalledText);
    }

    /// <summary>
    /// Echte Ausgabe von
    /// "cmd package query-activities --brief -a android.intent.action.MAIN
    /// -c android.intent.category.LAUNCHER" des Testgeräts (gekürzt).
    /// </summary>
    private const string LauncherQueryOutput = """
        52 activities found:
          Activity #0:
            priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true
            com.android.settings/.Settings
          Activity #1:
            priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true
            com.android.vending/.AssetBrowserActivity
          Activity #2:
            priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=true
            com.google.android.apps.maps/com.google.android.maps.MapsActivity
        """;

    [Fact]
    public void ParseLauncherPackages_ExtractsPackageNamesFromComponentLines()
    {
        var packages = PackageScanner.ParseLauncherPackages(LauncherQueryOutput);

        Assert.Equal(3, packages.Count);
        Assert.Contains("com.android.settings", packages);
        Assert.Contains("com.android.vending", packages);
        Assert.Contains("com.google.android.apps.maps", packages);
    }

    [Fact]
    public void ParseLauncherPackages_IgnoresHeaderAndEmptyOutput()
    {
        Assert.Empty(PackageScanner.ParseLauncherPackages(string.Empty));
        Assert.Empty(PackageScanner.ParseLauncherPackages("0 activities found:\n"));
    }

    // Echte Antwort von "dumpsys game" (TB311FU).
    private const string GameDumpOutput = """
        *Dump GameManagerService*

         No intervention found for package com.games.layerman

         No intervention found for package com.sega.sonicdash

         No intervention found for package com.mojang.minecraftpe

        """;

    [Fact]
    public void ParseGamePackages_ExtractsPackageNamesFromDump()
    {
        var packages = PackageScanner.ParseGamePackages(GameDumpOutput);

        Assert.Equal(3, packages.Count);
        Assert.Contains("com.games.layerman", packages);
        Assert.Contains("com.sega.sonicdash", packages);
        Assert.Contains("com.mojang.minecraftpe", packages);
    }

    [Fact]
    public void ParseGamePackages_IgnoresHeaderAndEmptyOutput()
    {
        Assert.Empty(PackageScanner.ParseGamePackages(string.Empty));
        Assert.Empty(PackageScanner.ParseGamePackages("*Dump GameManagerService*\n"));
    }

    [Fact]
    public void Category_IsUserInstalled_ForUserPackages()
    {
        var package = new AndroidPackage
        {
            PackageName = "com.example.game",
            Kind = PackageKind.User,
            HasLauncherEntry = true,
        };

        Assert.Equal(PackageCategory.UserInstalled, package.Category);
    }

    [Fact]
    public void Category_SplitsSystemPackages_ByLauncherEntry()
    {
        var app = new AndroidPackage
        {
            PackageName = "com.android.fmradio",
            Kind = PackageKind.System,
            HasLauncherEntry = true,
        };
        var service = new AndroidPackage
        {
            PackageName = "com.android.providers.media",
            Kind = PackageKind.System,
            HasLauncherEntry = false,
        };

        Assert.Equal(PackageCategory.SystemApp, app.Category);
        Assert.Equal(PackageCategory.SystemService, service.Category);
    }
}
