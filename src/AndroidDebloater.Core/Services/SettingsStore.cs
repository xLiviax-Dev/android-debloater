using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Lädt und speichert die Nutzereinstellungen (projektinfo.txt M8) als JSON und
/// überträgt sie in die Laufzeit-Schutzlisten (<see cref="ProtectionLists"/>).
/// Fehler beim Lesen führen nie zum Absturz: dann gilt der Standard.
/// </summary>
public static class SettingsStore
{
    /// <summary>Sprachen, die in dieser Version wählbar sind (Auswahl vorbereitet).</summary>
    public static IReadOnlyList<string> SupportedLanguages { get; } = new[] { AppSettings.DefaultLanguage };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Liest die Einstellungen aus dem Standardpfad; bei Fehlern die Standardwerte.</summary>
    public static AppSettings Load() => Load(AppPaths.SettingsFile);

    /// <summary>Liest die Einstellungen aus einer Datei; bei Fehlern die Standardwerte.</summary>
    public static AppSettings Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        try
        {
            if (!File.Exists(path))
            {
                return new AppSettings();
            }

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
        catch (Exception)
        {
            // Unlesbare/defekte Einstellungen dürfen den Start nicht verhindern.
            return new AppSettings();
        }
    }

    /// <summary>Schreibt die Einstellungen in den Standardpfad.</summary>
    public static Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        => SaveAsync(settings, AppPaths.SettingsFile, cancellationToken);

    /// <summary>Schreibt die Einstellungen in die angegebene Datei.</summary>
    public static async Task SaveAsync(
        AppSettings settings,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(settings, SerializerOptions);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Überträgt die Einstellungen in die Laufzeit: Whitelist (P2-5) und
    /// Experten-Mode (S-20). Wird beim Start und nach jeder Änderung aufgerufen.
    /// </summary>
    public static void Apply(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        ProtectionLists.SetWhitelist(settings.EffectiveWhitelist);
        ProtectionLists.SetExpertMode(settings.ExpertMode);
    }
}
