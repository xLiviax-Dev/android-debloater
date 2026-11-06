using System.Globalization;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Bewertet ein Paket als (mögliche) Bloatware und leitet daraus die
/// Gefahren-Einstufung ab (projektinfo.txt Abschnitt 6). Die Bewertung ist
/// bewusst transparent: alle Gründe werden mitgeliefert und im UI angezeigt.
/// Es wird nie automatisch entfernt – die Entscheidung bleibt beim Nutzer.
/// </summary>
public static class BloatwareClassifier
{
    /// <summary>Ab diesem Score gilt ein Paket als Bloatware-Kandidat.</summary>
    public const int BloatThreshold = 50;

    /// <summary>Hersteller-/Plattform-Pakete, die zum System-Image gehören (kein Bloat-Verdacht).</summary>
    private static readonly string[] SystemImagePrefixes =
    {
        "android.",
        "com.android.",
        "com.google.",
        "com.mediatek",
        "com.lenovo",
        "com.tblenovo",
        "com.zui",
        "com.motorola",
        "com.qualcomm",
        "com.arm.",
        "com.dolby",
        "com.lgsi",
        "com.longcheer",
    };

    /// <summary>Namensbestandteile, die für sich genommen schon stark auf Bloatware hindeuten.</summary>
    private static readonly string[] StrongBloatPatterns =
    {
        "whatsnew", "oobe", "promo", "trial", "demo", "screensaver", "drama", "prank",
    };

    /// <summary>Namensbestandteile, die allein noch kein Bloat sind (nur ein Hinweis).</summary>
    private static readonly string[] WeakBloatPatterns =
    {
        "feedback", "recommend", "suggest", "tips",
    };

    /// <summary>Bekannte OEM-Promo-/Preload-Pakete (aus dem Auto-Install-Layout bzw. verifiziert).</summary>
    private static readonly HashSet<string> KnownPromo = new(StringComparer.Ordinal)
    {
        "com.einnovation.temu",
        "com.zhiliaoapp.musically",
        "cn.wps.moffice_eng",
        "com.mobilityware.solitaire",
        "com.opera.browser",
        "com.opera.preinstall",
        "net.wooga.junes_journey",
        "ai.perplexity.app.android",
    };

    /// <summary>
    /// Bewertet ein Paket: setzt <see cref="AndroidPackage.Safety"/>,
    /// <see cref="AndroidPackage.BloatScore"/>, <see cref="AndroidPackage.BloatReasons"/>
    /// und <see cref="AndroidPackage.IsProtected"/>.
    /// </summary>
    public static void Assess(AndroidPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        // 1) Kritische Pakete (Abschnitt 6.1): technisch gesperrt.
        if (ProtectionLists.IsCritical(package.PackageName))
        {
            package.Safety = PackageSafety.Critical;
            package.IsProtected = true;
            package.BloatScore = 0;
            package.BloatReasons = new[] { Strings.ReasonCritical };
            return;
        }

        // 2) Whitelist: geschützt, aber unbedenklich.
        if (ProtectionLists.IsWhitelisted(package.PackageName))
        {
            package.Safety = PackageSafety.Safe;
            package.IsProtected = true;
            package.BloatScore = 0;
            package.BloatReasons = new[] { Strings.ReasonWhitelist };
            return;
        }

        // 3) Vom Nutzer nachträglich installiert -> kein OEM-Bloat, unbedenklich.
        if (package.Kind == PackageKind.User)
        {
            package.Safety = PackageSafety.Safe;
            package.BloatScore = 0;
            package.BloatReasons = new[]
            {
                package.InstallerPackage is null
                    ? Strings.ReasonUserInstalled
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.ReasonUserInstalledViaFormat,
                        package.InstallerPackage),
            };
            return;
        }

        // 4) Reine System-/Konfigurationsbestandteile ohne Nutzerbezug (Framework
        //    "android", Overlays, Chipsatz-Pakete): kein Bloat, aber vorsichtig.
        if (IsSystemInternal(package.PackageName))
        {
            package.Safety = PackageSafety.Caution;
            package.BloatScore = 0;
            package.BloatReasons = new[] { Strings.ReasonSystemInternal };
            return;
        }

        // Ab hier: vorinstallierte System-App. Einzelne Signale werden aufsummiert.
        bool isSystemImage = IsSystemImage(package.PackageName);
        bool isKnownPromo = KnownPromo.Contains(package.PackageName);
        string? strongPattern = MatchNamePattern(package.PackageName, StrongBloatPatterns);
        string? weakPattern = MatchNamePattern(package.PackageName, WeakBloatPatterns);

        var reasons = new List<string>();
        int score = 0;

        if (isSystemImage)
        {
            reasons.Add(Strings.ReasonSystemImage);
        }
        else
        {
            score += 50;
            reasons.Add(Strings.ReasonOemPreload);
            if (package.InstallerPackage == "com.android.vending")
            {
                reasons.Add(Strings.ReasonPlayAutoInstall);
            }
        }

        if (strongPattern is not null)
        {
            score += 50;
            reasons.Add(string.Format(
                CultureInfo.CurrentCulture,
                Strings.ReasonStrongPatternFormat,
                strongPattern));
        }

        if (weakPattern is not null)
        {
            score += 20;
            reasons.Add(string.Format(
                CultureInfo.CurrentCulture,
                Strings.ReasonWeakPatternFormat,
                weakPattern));
        }

        if (isKnownPromo)
        {
            score += 60;
            reasons.Add(Strings.ReasonKnownPromo);
        }

        package.BloatScore = Math.Clamp(score, 0, 100);
        package.BloatReasons = reasons;

        // Stufe (6.2): erkennbare Bloatware = unbedenklich; sonst System-/OEM-Dienst
        // mit unklarer Funktion -> im Zweifel Vorsicht.
        package.Safety = package.IsBloatCandidate ? PackageSafety.Safe : PackageSafety.Caution;
    }

    /// <summary>Pakete, die reine System-/Konfigurationsbestandteile sind.</summary>
    private static bool IsSystemInternal(string name)
        => name == "android"
        || name == "com.mediatek"
        || name.StartsWith("android.", StringComparison.Ordinal)
        || name.StartsWith("com.android.", StringComparison.Ordinal)
        || name.StartsWith("com.mediatek.", StringComparison.Ordinal)
        || name.Contains(".overlay", StringComparison.Ordinal)
        || name.StartsWith("com.google.android.overlay", StringComparison.Ordinal);

    /// <summary>Gehört das Paket zum Hersteller-/Plattform-Image (Google, Lenovo, Chipsatz …)?</summary>
    private static bool IsSystemImage(string name)
    {
        foreach (var prefix in SystemImagePrefixes)
        {
            if (name.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string? MatchNamePattern(string name, string[] patterns)
    {
        foreach (var pattern in patterns)
        {
            if (name.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                return pattern;
            }
        }

        return null;
    }
}
