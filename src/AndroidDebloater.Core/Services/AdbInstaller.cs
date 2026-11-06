using System.IO.Compression;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Beschafft ADB nach dem festgelegten Ablauf (Abschnitt 4.1 / F10):
/// Version + SHA-1 aus dem offiziellen Google-Manifest lesen, die
/// versionierte ZIP laden, SHA-1 + Größe wirklich prüfen, entpacken und
/// mit "adb --version" verifizieren. Ablage unter %LOCALAPPDATA%.
/// </summary>
public sealed class AdbInstaller
{
    private static readonly HttpClient SharedClient = CreateClient();

    private readonly HttpClient _httpClient;

    /// <summary>Erzeugt einen Installer; ohne Angabe wird ein gemeinsamer HttpClient benutzt.</summary>
    public AdbInstaller(HttpClient? httpClient = null)
        => _httpClient = httpClient ?? SharedClient;

    /// <summary>Liest die aktuelle platform-tools-Version aus dem Manifest.</summary>
    public async Task<PlatformToolsRelease> GetReleaseAsync(CancellationToken cancellationToken = default)
    {
        var xml = await _httpClient
            .GetStringAsync(AdbEndpoints.SdkRepositoryManifest, cancellationToken)
            .ConfigureAwait(false);

        return PlatformToolsManifestReader.Parse(xml, AdbEndpoints.PlatformToolsPackagePath);
    }

    /// <summary>Liefert das Archiv für das aktuelle Betriebssystem.</summary>
    public static AdbReleaseArchive SelectArchive(PlatformToolsRelease release)
        => release.ForHost(HostOperatingSystemExtensions.Current)
           ?? throw new InvalidOperationException(
               $"Kein platform-tools-Archiv fuer {HostOperatingSystemExtensions.Current} im Manifest gefunden.");

    /// <summary>Führt den vollständigen Installationsvorgang aus.</summary>
    public async Task<AdbStatus> InstallAsync(
        IProgress<AdbInstallProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report(new AdbInstallProgress(AdbInstallStage.ReadingManifest));

        var release = await GetReleaseAsync(cancellationToken).ConfigureAwait(false);
        var archive = SelectArchive(release);

        var zipPath = Path.Combine(Path.GetTempPath(), "AndroidDebloater-" + archive.FileName);
        var stagingDir = Path.Combine(AppPaths.AppFolder, "staging-" + Guid.NewGuid().ToString("N"));

        try
        {
            progress?.Report(new AdbInstallProgress(AdbInstallStage.Downloading, 0, archive.SizeBytes));
            await DownloadAsync(
                AdbEndpoints.DownloadUrl(archive.FileName),
                zipPath,
                archive.SizeBytes,
                progress,
                cancellationToken).ConfigureAwait(false);

            progress?.Report(new AdbInstallProgress(AdbInstallStage.VerifyingChecksum));
            await VerifyChecksumAsync(zipPath, archive, cancellationToken).ConfigureAwait(false);

            progress?.Report(new AdbInstallProgress(AdbInstallStage.Extracting));
            ExtractToManagedFolder(zipPath, stagingDir);

            progress?.Report(new AdbInstallProgress(AdbInstallStage.VerifyingInstallation));
            var managedAdb = AppPaths.ManagedAdbPath;
            var runner = new AdbRunner(managedAdb);
            var versionText = await runner.GetVersionTextAsync(cancellationToken).ConfigureAwait(false);
            var version = AdbRunner.ParseVersion(versionText)
                ?? throw new InvalidOperationException(
                    "Das installierte adb meldet keine Version ('adb --version' fehlgeschlagen).");

            progress?.Report(new AdbInstallProgress(AdbInstallStage.Completed));
            return new AdbStatus(true, managedAdb, version, AdbSource.AppManaged, versionText);
        }
        finally
        {
            TryDeleteFile(zipPath);
            TryDeleteDirectory(stagingDir);
        }
    }

    private async Task DownloadAsync(
        string url,
        string destinationPath,
        long expectedSize,
        IProgress<AdbInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? expectedSize;

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var target = File.Create(destinationPath);

        var buffer = new byte[81920];
        long received = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
            received += read;
            progress?.Report(new AdbInstallProgress(AdbInstallStage.Downloading, received, total));
        }
    }

    private static async Task VerifyChecksumAsync(
        string zipPath,
        AdbReleaseArchive archive,
        CancellationToken cancellationToken)
    {
        var actualSha1 = await Checksums.ComputeSha1Async(zipPath, cancellationToken).ConfigureAwait(false);
        if (!string.Equals(actualSha1, archive.Sha1, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"SHA-1 stimmt nicht: erwartet {archive.Sha1}, erhalten {actualSha1}.");
        }

        var actualSize = new FileInfo(zipPath).Length;
        if (archive.SizeBytes > 0 && actualSize != archive.SizeBytes)
        {
            throw new InvalidDataException(
                $"Dateigroesse stimmt nicht: erwartet {archive.SizeBytes}, erhalten {actualSize}.");
        }
    }

    private static void ExtractToManagedFolder(string zipPath, string stagingDir)
    {
        if (Directory.Exists(stagingDir))
        {
            Directory.Delete(stagingDir, true);
        }

        Directory.CreateDirectory(stagingDir);
        ZipFile.ExtractToDirectory(zipPath, stagingDir);

        // Die ZIP enthält einen Ordner "platform-tools"; sonst die Wurzel nehmen.
        var extracted = Path.Combine(stagingDir, "platform-tools");
        if (!Directory.Exists(extracted))
        {
            extracted = stagingDir;
        }

        var target = AppPaths.PlatformToolsFolder;
        var parent = Path.GetDirectoryName(target);
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }

        if (Directory.Exists(target))
        {
            Directory.Delete(target, true);
        }

        Directory.Move(extracted, target);
        EnsureExecutable(target);
    }

    private static void EnsureExecutable(string folder)
    {
        // Unter Windows nicht nötig; unter Linux/macOS muss der exec-Bit gesetzt sein.
        if (OperatingSystem.IsWindows() || !File.Exists(AppPaths.ManagedAdbPath))
        {
            return;
        }

        const UnixFileMode mode =
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

        File.SetUnixFileMode(AppPaths.ManagedAdbPath, mode);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
            // Aufräumen darf den eigentlichen Ablauf nicht stören.
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
            // Aufräumen darf den eigentlichen Ablauf nicht stören.
        }
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AndroidDebloater/0.1");
        return client;
    }
}
