using System.Text.Json;
using System.Text.Json.Serialization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Sichert und lädt das Wiederherstellungs-Profil als JSON
/// (projektinfo.txt S-04). Ablage unter <see cref="AppPaths.RollbackFolder"/>.
/// </summary>
public static class RollbackProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Dateiname des Profils, eindeutig je Ausführungszeitpunkt.</summary>
    public static string CreateFileName(DateTimeOffset timestamp)
        => $"rollback-{timestamp:yyyyMMdd-HHmmss-fff}.json";

    /// <summary>Schreibt das Profil als lesbares JSON an den angegebenen Pfad.</summary>
    public static async Task SaveAsync(
        RollbackProfile profile,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        EnsureDirectory(path);

        var json = JsonSerializer.Serialize(profile, Options);
        await File.WriteAllTextAsync(path, json, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Liest ein Profil aus einer JSON-Datei; null, wenn sie fehlt.</summary>
    public static async Task<RollbackProfile?> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        return JsonSerializer.Deserialize<RollbackProfile>(json, Options);
    }

    private static void EnsureDirectory(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
