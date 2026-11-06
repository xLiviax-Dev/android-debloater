namespace AndroidDebloater.Core.Services;

/// <summary>
/// Merkt sich lokal, welche Pakete das Gerät als Spiel führt (ApplicationInfo-Flag
/// IS_GAME). Das Gerät meldet über "dumpsys game" nur INSTALLIERTE Spiele; entfernte
/// Spiele (z. B. nach "pm uninstall --user 0 -k") fehlen dort. Damit ein Spiel auch im
/// Tab "Removed" ein Spiel bleibt, wird die Liste fortgeschrieben: bekannte Einträge
/// bleiben erhalten, neu erkannte kommen hinzu. Abgelegt wird eine Paketname pro Zeile.
/// </summary>
public static class GameRegistry
{
    /// <summary>Datei mit den gemerkten Spiele-Paketen (ein Paketname pro Zeile).</summary>
    public static string RegistryFile => Path.Combine(AppPaths.AppFolder, "games.txt");

    /// <summary>Liest die gemerkten Spiele-Pakete aus dem Standardpfad.</summary>
    public static IReadOnlyCollection<string> Load() => Load(RegistryFile);

    /// <summary>Liest die gemerkten Spiele-Pakete; bei Fehlern eine leere Menge.</summary>
    public static IReadOnlyCollection<string> Load(string path)
    {
        var games = new HashSet<string>(StringComparer.Ordinal);

        try
        {
            if (!File.Exists(path))
            {
                return games;
            }

            foreach (var raw in File.ReadLines(path))
            {
                var name = raw.Trim();
                if (name.Length > 0)
                {
                    games.Add(name);
                }
            }
        }
        catch (Exception)
        {
            // Eine unlesbare Datei bedeutet schlicht "nichts gemerkt".
        }

        return games;
    }

    /// <summary>Vereinigt die gemerkten Spiele mit den erkannten und schreibt sie zurück.</summary>
    public static IReadOnlyCollection<string> Merge(IEnumerable<string> detected)
        => Merge(detected, RegistryFile);

    /// <summary>
    /// Vereinigt die in <paramref name="path"/> gemerkten Spiele mit den erkannten und
    /// schreibt das Ergebnis zurück. Liefert die vollständige Menge.
    /// </summary>
    public static IReadOnlyCollection<string> Merge(IEnumerable<string> detected, string path)
    {
        ArgumentNullException.ThrowIfNull(detected);

        var games = new HashSet<string>(Load(path), StringComparer.Ordinal);
        int before = games.Count;

        foreach (var name in detected)
        {
            games.Add(name);
        }

        if (games.Count != before)
        {
            Save(games, path);
        }

        return games;
    }

    /// <summary>Löscht die gemerkte Spiele-Liste (eigener Knopf in den Einstellungen).</summary>
    public static void Clear()
    {
        try
        {
            if (File.Exists(RegistryFile))
            {
                File.Delete(RegistryFile);
            }
        }
        catch (Exception)
        {
            // Nicht löschbar: der nächste Merge überschreibt die Datei ohnehin.
        }
    }

    private static void Save(IEnumerable<string> games, string path)
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppFolder);
            File.WriteAllLines(path, games.OrderBy(name => name, StringComparer.Ordinal));
        }
        catch (Exception)
        {
            // Ein nicht schreibbarer Ordner darf den Scan nicht verhindern.
        }
    }
}
