using System.Text;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services.Apk;

namespace AndroidDebloater.Core.Services;

/// <summary>Ergebnis einer Icon-Abfrage: Bilddaten (falls gefunden) und der Anzeigename.</summary>
public readonly record struct ApkIconResult(byte[]? Image, string? Label)
{
    public static ApkIconResult None => new(null, null);

    public bool HasImage => Image is not null;
}

/// <summary>
/// Holt das Launcher-Icon eines Pakets direkt vom Gerät. Ablauf:
/// pm path -> base.apk -> AndroidManifest.xml -> Icon-Verweis -> resources.arsc -> Bildpfad
/// und schließlich "unzip -p" auf dem Gerät. Die APK selbst wird nie übertragen.
/// </summary>
public sealed class ApkIconExtractor
{
    private const string ManifestEntry = "AndroidManifest.xml";
    private const string ResourcesEntry = "resources.arsc";

    private static readonly TimeSpan ShellTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ReadTimeout = TimeSpan.FromSeconds(45);

    private readonly AdbRunner _adb;

    public ApkIconExtractor(AdbRunner adb) => _adb = adb;

    public async Task<ApkIconResult> ExtractAsync(AndroidPackage package, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(package);

        var apk = await ResolveBaseApkAsync(package, cancellationToken).ConfigureAwait(false);
        if (apk is null)
        {
            return ApkIconResult.None;
        }

        var manifest = await ReadEntryAsync(apk, ManifestEntry, cancellationToken).ConfigureAwait(false);
        if (manifest is null)
        {
            return ApkIconResult.None;
        }

        var info = ManifestIconReader.Read(manifest);

        // Die arsc ist groß und wird daher nur einmal und nur bei Bedarf geladen.
        byte[]? arsc = null;
        async Task<byte[]?> LoadArscAsync()
            => arsc ??= await ReadEntryAsync(apk, ResourcesEntry, cancellationToken).ConfigureAwait(false);

        var image = await ResolveAsync(apk, info.IconResourceId, info.IconPath, LoadArscAsync, cancellationToken)
                        .ConfigureAwait(false)
                    ?? await FallbackAsync(apk, cancellationToken).ConfigureAwait(false);

        var label = info.Label;
        if (string.IsNullOrWhiteSpace(label) && info.LabelResourceId is uint labelId)
        {
            var table = await LoadArscAsync().ConfigureAwait(false);
            if (table is not null)
            {
                label = ArscResolver.Resolve(table, labelId);
            }
        }

        return new ApkIconResult(image, label);
    }

    /// <summary>Ermittelt den Pfad der Basis-APK; bei Splits liegt das Icon immer in base.apk.</summary>
    private async Task<string?> ResolveBaseApkAsync(AndroidPackage package, CancellationToken cancellationToken)
    {
        var result = await _adb
            .RunShellAsync(new[] { "pm", "path", package.PackageName }, ShellTimeout, cancellationToken)
            .ConfigureAwait(false);

        string? first = null;
        foreach (var raw in result.StandardOutput.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("package:", StringComparison.Ordinal))
            {
                continue;
            }

            var path = line["package:".Length..];
            if (path.EndsWith("base.apk", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }

            first ??= path;
        }

        if (first is not null)
        {
            return first;
        }

        // Versteckte oder deaktivierte Pakete liefern bei "pm path" nichts;
        // der codePath aus dumpsys zeigt weiterhin auf das vorhandene Verzeichnis.
        var codePath = package.CodePath;
        if (string.IsNullOrWhiteSpace(codePath))
        {
            return null;
        }

        return codePath.EndsWith(".apk", StringComparison.OrdinalIgnoreCase)
            ? codePath
            : codePath.TrimEnd('/') + "/base.apk";
    }

    private async Task<byte[]?> ReadEntryAsync(string apkPath, string entry, CancellationToken cancellationToken)
    {
        var bytes = await _adb
            .RunExecOutAsync(new[] { "unzip", "-p", apkPath, entry }, ReadTimeout, cancellationToken)
            .ConfigureAwait(false);

        return bytes.Length == 0 ? null : bytes;
    }

    /// <summary>
    /// Folgt der Icon-Referenz: direkter Dateipfad, Ressourcen-ID über die arsc,
    /// und bei adaptiven Icons zusätzlich der Verweis aus dem Icon-XML.
    /// </summary>
    private async Task<byte[]?> ResolveAsync(
        string apk,
        uint? resourceId,
        string? path,
        Func<Task<byte[]?>> loadArsc,
        CancellationToken cancellationToken)
    {
        byte[]? arsc = null;

        for (int depth = 0; depth < 3; depth++)
        {
            if (!string.IsNullOrEmpty(path))
            {
                if (ResChunk.IsImagePath(path))
                {
                    var image = await ReadEntryAsync(apk, path, cancellationToken).ConfigureAwait(false);
                    if (image is not null && ResChunk.LooksLikeImage(image))
                    {
                        return image;
                    }
                }
                else if (path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    // Adaptives Icon: Vordergrund- bzw. Hintergrundzeichnung ermitteln.
                    var xml = await ReadEntryAsync(apk, path, cancellationToken).ConfigureAwait(false);
                    resourceId = xml is null ? null : AdaptiveIconReader.ReadDrawableReference(xml);
                }

                path = null;
            }

            if (resourceId is not uint id)
            {
                return null;
            }

            arsc ??= await loadArsc().ConfigureAwait(false);
            if (arsc is null)
            {
                return null;
            }

            var resolved = ArscResolver.Resolve(arsc, id);
            if (resolved is null)
            {
                return null;
            }

            path = resolved;
        }

        return null;
    }

    /// <summary>Notnagel: passendes Bild im APK-Inhaltsverzeichnis suchen (höchste Dichte gewinnt).</summary>
    private async Task<byte[]?> FallbackAsync(string apk, CancellationToken cancellationToken)
    {
        var listing = await _adb
            .RunExecOutAsync(new[] { "unzip", "-l", apk }, ReadTimeout, cancellationToken)
            .ConfigureAwait(false);

        if (listing.Length == 0)
        {
            return null;
        }

        string? bestEntry = null;
        int bestScore = -1;

        foreach (var raw in Encoding.UTF8.GetString(listing).Split('\n'))
        {
            foreach (var token in raw.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            {
                if (!token.StartsWith("res/", StringComparison.Ordinal) || !ResChunk.IsImagePath(token))
                {
                    continue;
                }

                int score = DensityScore(token);
                if (token.Contains("ic_launcher", StringComparison.OrdinalIgnoreCase))
                {
                    score += 100;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    bestEntry = token;
                }
            }
        }

        if (bestEntry is null)
        {
            return null;
        }

        var bytes = await ReadEntryAsync(apk, bestEntry, cancellationToken).ConfigureAwait(false);
        return bytes is not null && ResChunk.LooksLikeImage(bytes) ? bytes : null;
    }

    private static int DensityScore(string path)
        => path.Contains("xxxhdpi", StringComparison.OrdinalIgnoreCase) ? 640
            : path.Contains("xxhdpi", StringComparison.OrdinalIgnoreCase) ? 480
            : path.Contains("xhdpi", StringComparison.OrdinalIgnoreCase) ? 320
            : path.Contains("hdpi", StringComparison.OrdinalIgnoreCase) ? 240
            : path.Contains("mdpi", StringComparison.OrdinalIgnoreCase) ? 160
            : path.Contains("ldpi", StringComparison.OrdinalIgnoreCase) ? 120
            : 0;
}
