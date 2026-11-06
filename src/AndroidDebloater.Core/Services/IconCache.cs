using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>Ergebnis des Icon-Nachladens: PNG-Bild (falls vorhanden) und Anzeigename.</summary>
public readonly record struct IconLoadResult(byte[]? Png, string? Label)
{
    public static IconLoadResult None => new(null, null);

    public bool HasImage => Png is { Length: > 0 };
}

/// <summary>Belegung des Icon-Caches: Anzahl der Dateien und Gesamtgröße in Bytes.</summary>
public readonly record struct IconCacheUsage(int FileCount, long TotalBytes)
{
    /// <summary>Leerer bzw. nicht vorhandener Cache.</summary>
    public static IconCacheUsage Empty => new(0, 0);
}

/// <summary>
/// Lädt App-Icons und Anzeigenamen vom Gerät nach und legt beides als PNG bzw. Textdatei
/// unter <c>%LOCALAPPDATA%\Android Debloater\icons</c> ab, damit weitere Starts kein ADB
/// mehr benötigen. Das Laden erfolgt bewusst verzögert (nur auf Anforderung).
/// </summary>
public static class IconCache
{
    // Begrenzt parallele Gerätezugriffe, damit ADB nicht überlastet wird.
    private static readonly SemaphoreSlim Gate = new(2);

    /// <summary>Ordner, in dem die Icons und Namen zwischengespeichert werden.</summary>
    public static string CacheDirectory => AppPaths.IconCacheFolder;

    /// <summary>Ermittelt Anzahl und Gesamtgröße der zwischengespeicherten Dateien (M8).</summary>
    public static IconCacheUsage GetUsage()
    {
        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                return IconCacheUsage.Empty;
            }

            int count = 0;
            long bytes = 0;
            foreach (var file in Directory.EnumerateFiles(CacheDirectory))
            {
                count++;
                try
                {
                    bytes += new FileInfo(file).Length;
                }
                catch
                {
                    // Eine einzelne unlesbare Datei darf die Anzeige nicht verhindern.
                }
            }

            return new IconCacheUsage(count, bytes);
        }
        catch (Exception)
        {
            return IconCacheUsage.Empty;
        }
    }

    /// <summary>Löscht den Icon-Cache und liefert die Anzahl der entfernten Dateien (M8).</summary>
    public static int Clear()
    {
        int removed = 0;

        try
        {
            if (!Directory.Exists(CacheDirectory))
            {
                return 0;
            }

            foreach (var file in Directory.EnumerateFiles(CacheDirectory))
            {
                try
                {
                    File.Delete(file);
                    removed++;
                }
                catch
                {
                    // Eine gesperrte Datei wird beim nächsten Mal erneut versucht.
                }
            }
        }
        catch (Exception)
        {
            // Ein nicht lesbarer Ordner bedeutet schlicht "nichts entfernt".
        }

        return removed;
    }

    /// <summary>Liefert Icon und Anzeigename eines Pakets (aus dem Cache oder frisch vom Gerät).</summary>
    public static async Task<IconLoadResult> LoadAsync(
        AdbRunner adb,
        AndroidPackage package,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(adb);
        ArgumentNullException.ThrowIfNull(package);

        string baseName = Sanitize(package.PackageName);
        string imageFile = Path.Combine(CacheDirectory, baseName + ".png");
        string labelFile = Path.Combine(CacheDirectory, baseName + ".label");

        var (png, label) = await TryReadCacheAsync(imageFile, labelFile, cancellationToken).ConfigureAwait(false);

        if (png is null || label is null)
        {
            await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                (png, label) = await TryReadCacheAsync(imageFile, labelFile, cancellationToken).ConfigureAwait(false);

                if (png is null || label is null)
                {
                    var extractor = new ApkIconExtractor(adb);
                    var result = await extractor.ExtractAsync(package, cancellationToken).ConfigureAwait(false);

                    if (png is null && result.Image is not null)
                    {
                        png = SkiaImageDecoder.ToPng(result.Image);
                        if (png is not null)
                        {
                            await TryWriteBytesAsync(imageFile, png, cancellationToken).ConfigureAwait(false);
                        }
                    }

                    if (label is null && result.Label is not null)
                    {
                        label = result.Label.Trim();
                        await TryWriteTextAsync(labelFile, label, cancellationToken).ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                Gate.Release();
            }
        }

        return new IconLoadResult(png, label);
    }

    private static async Task<(byte[]? Image, string? Label)> TryReadCacheAsync(
        string imageFile,
        string labelFile,
        CancellationToken cancellationToken)
    {
        byte[]? image = null;
        string? label = null;

        try
        {
            if (File.Exists(imageFile))
            {
                image = await File.ReadAllBytesAsync(imageFile, cancellationToken).ConfigureAwait(false);
            }
        }
        catch
        {
            // Cache unlesbar -> wird neu erzeugt.
        }

        try
        {
            if (File.Exists(labelFile))
            {
                label = (await File.ReadAllTextAsync(labelFile, cancellationToken).ConfigureAwait(false)).Trim();
            }
        }
        catch
        {
            // Cache unlesbar -> wird neu erzeugt.
        }

        return (image, label);
    }

    private static async Task TryWriteBytesAsync(string file, byte[] data, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllBytesAsync(file, data, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Ein nicht schreibbarer Cache darf das Laden nicht verhindern.
        }
    }

    private static async Task TryWriteTextAsync(string file, string text, CancellationToken cancellationToken)
    {
        try
        {
            Directory.CreateDirectory(CacheDirectory);
            await File.WriteAllTextAsync(file, text, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // siehe oben
        }
    }

    private static string Sanitize(string packageName)
    {
        var chars = packageName.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(Path.GetInvalidFileNameChars(), chars[i]) >= 0)
            {
                chars[i] = '_';
            }
        }

        return new string(chars);
    }
}
