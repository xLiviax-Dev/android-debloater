using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Core.Models;

/// <summary>
/// Vom Nutzer einstellbare Optionen (projektinfo.txt M8). Gespeichert als JSON
/// unter <see cref="AppPaths.SettingsFile"/>; geladen und angewendet über
/// <see cref="SettingsStore"/>.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Sprachkennung der Oberfläche; derzeit ist nur Englisch verfügbar.</summary>
    public const string DefaultLanguage = "en";

    /// <summary>
    /// Experten-Mode (S-20): macht CRITICAL-Pakete bewusst bearbeitbar.
    /// Standard AUS – die harte Sperre (S-07) gilt, solange er nicht
    /// eingeschaltet wurde.
    /// </summary>
    public bool ExpertMode { get; set; }

    /// <summary>Anzeigesprache (Auswahl vorbereitet; aktuell nur "en").</summary>
    public string Language { get; set; } = DefaultLanguage;

    /// <summary>
    /// Farbschema der Oberfläche: "Light" oder "Dark". null = dem System folgen.
    /// Wird über den Umschalter im Kopf der Hauptansicht gesetzt.
    /// </summary>
    public string? Theme { get; set; }

    /// <summary>
    /// Eigener Pfad zur adb-Datei; null/leer bedeutet automatische Erkennung
    /// über <see cref="AdbLocator"/>.
    /// </summary>
    public string? AdbPath { get; set; }

    /// <summary>
    /// Nutzer-Whitelist (P2-5). null = Standard-Set aus
    /// <see cref="ProtectionLists.DefaultWhitelist"/> verwenden; eine leere
    /// Liste bedeutet bewusst "keine Einträge".
    /// </summary>
    public List<string>? Whitelist { get; set; }

    /// <summary>
    /// Gültige Whitelist: der eigene Satz, sonst das Standard-Set.
    /// </summary>
    public IReadOnlyList<string> EffectiveWhitelist =>
        Whitelist ?? ProtectionLists.DefaultWhitelist.ToList();
}
