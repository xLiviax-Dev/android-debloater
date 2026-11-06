namespace AndroidDebloater.Core.Services;

/// <summary>
/// Schutzlisten aus projektinfo.txt Abschnitt 6.1.
/// Die Einträge sind bewusst KONKRETE Paketnamen – KEINE Präfix-Muster
/// (Phase-2-Entscheidung A3).
/// <para>
/// <see cref="DefaultWhitelist"/> und <see cref="Critical"/> sind die
/// voreingestellten Sätze. Die tatsächlich gültige Whitelist ist zur Laufzeit
/// änderbar (M8 / P2-5) und wird über <see cref="SetWhitelist"/> gesetzt; sie
/// startet immer mit dem Standard-Set.
/// </para>
/// </summary>
public static class ProtectionLists
{
    private static readonly object Gate = new();

    /// <summary>Standard-Whitelist: geschützt, nie entfernbar (Ausgangswert).</summary>
    public static readonly IReadOnlySet<string> DefaultWhitelist =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "com.android.vending",                 // Google Play Store
            "com.google.android.gms",              // Google Play-Dienste
            "com.google.android.gms.supervision",  // Family-Link-Agent
        };

    /// <summary>Feste Sperrliste: kritische Pakete, technisch gesperrt (Abschnitt 6.1).</summary>
    public static readonly IReadOnlySet<string> Critical =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "com.example.android.factoryreset",    // Werksreset (MASTER_CLEAR)
            "com.android.systemui",                // System-UI
            "com.android.providers.telephony",     // Telefonie-Provider
            "com.android.phone",                   // Telefonie
            "com.android.server.telecom",          // Telefonie-Dienst
            "com.android.packageinstaller",        // Paket-Installer
            "com.google.android.packageinstaller", // Paket-Installer (Google)
            "com.android.settings",                // Einstellungen
            "com.android.providers.settings",      // Einstellungen-Provider
        };

    private static HashSet<string> _whitelist = new(DefaultWhitelist, StringComparer.Ordinal);

    /// <summary>Die aktuell gültige Whitelist (Standard-Set oder Nutzerauswahl).</summary>
    public static IReadOnlyList<string> Whitelist
    {
        get
        {
            lock (Gate)
            {
                return _whitelist.ToArray();
            }
        }
    }

    /// <summary>
    /// Experten-Mode (S-20): solange AUS, ist <see cref="Critical"/> hart
    /// gesperrt (S-07). Ausschalten stellt die harte Sperre sofort wieder her.
    /// </summary>
    public static bool ExpertModeEnabled { get; private set; }

    /// <summary>True, wenn das Paket auf der aktuell gültigen Whitelist steht.</summary>
    public static bool IsWhitelisted(string packageName)
    {
        lock (Gate)
        {
            return _whitelist.Contains(packageName);
        }
    }

    /// <summary>True, wenn das Paket auf der festen Sperrliste steht.</summary>
    public static bool IsCritical(string packageName) => Critical.Contains(packageName);

    /// <summary>Setzt die gültige Whitelist (M8 / P2-5). Doppelte werden entfernt.</summary>
    public static void SetWhitelist(IEnumerable<string> packageNames)
    {
        ArgumentNullException.ThrowIfNull(packageNames);

        var next = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in packageNames)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                next.Add(name.Trim());
            }
        }

        lock (Gate)
        {
            _whitelist = next;
        }
    }

    /// <summary>Schaltet den Experten-Mode ein oder aus (S-20).</summary>
    public static void SetExpertMode(bool enabled) => ExpertModeEnabled = enabled;

    /// <summary>Setzt Whitelist und Experten-Mode auf den Auslieferungszustand zurück (Start/Tests).</summary>
    public static void Reset()
    {
        SetWhitelist(DefaultWhitelist);
        SetExpertMode(false);
    }
}
