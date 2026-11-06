using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Kapselt Aufrufe der ADB-Ausführungsdatei. Es wird immer der explizit
/// übergebene Pfad benutzt (kein Verlass auf PATH zur Laufzeit). Optional
/// wird jedem Aufruf ein Zielgerät (<c>-s &lt;serial&gt;</c>) vorangestellt.
/// </summary>
public sealed class AdbRunner
{
    /// <summary>Vollständiger Pfad zur adb-Datei.</summary>
    public string AdbPath { get; }

    /// <summary>Seriennummer des Zielgeräts; null für den Geräte-unabhängigen Aufruf.</summary>
    public string? Serial { get; set; }

    /// <summary>Erzeugt einen Runner für die angegebene adb-Datei.</summary>
    public AdbRunner(string adbPath)
        => AdbPath = adbPath ?? throw new ArgumentNullException(nameof(adbPath));

    /// <summary>Führt adb mit den angegebenen Argumenten ohne Zeitlimit aus.</summary>
    public Task<ProcessResult> RunAsync(
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
        => RunAsync(arguments, timeout: null, cancellationToken);

    /// <summary>Führt adb mit den angegebenen Argumenten und optionalem Zeitlimit aus.</summary>
    public async Task<ProcessResult> RunAsync(
        IEnumerable<string> arguments,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        using var timeoutSource = timeout is { } limit
            ? CreateTimeoutSource(cancellationToken, limit)
            : null;

        var token = timeoutSource?.Token ?? cancellationToken;
        return await ProcessRunner.RunAsync(AdbPath, WithSerial(arguments), token).ConfigureAwait(false);
    }

    /// <summary>
    /// Führt einen Geräte-Shell-Befehl aus. Die Befehlsbestandteile werden
    /// einzeln übergeben und von adb zu einer Shell-Zeile zusammengesetzt.
    /// </summary>
    public Task<ProcessResult> RunShellAsync(
        IEnumerable<string> command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "shell" };
        arguments.AddRange(command);
        return RunAsync(arguments, timeout, cancellationToken);
    }

    /// <summary>Ruft "adb --version" auf und liefert die vollständige Ausgabe.</summary>
    public async Task<string> GetVersionTextAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunAsync(new[] { "--version" }, cancellationToken).ConfigureAwait(false);
        return (result.StandardOutput + result.StandardError).Trim();
    }

    /// <summary>
    /// Führt <c>adb exec-out</c> aus und liefert die Rohausgabe als Bytes.
    /// Wird für Binärübertragungen gebraucht (z. B. <c>unzip -p</c> auf dem Gerät).
    /// Der Befehl läuft direkt, ohne Shell – dadurch keine Interpretation der Argumente.
    /// </summary>
    public Task<byte[]> RunExecOutAsync(
        IEnumerable<string> command,
        TimeSpan? timeout = null,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "exec-out" };
        arguments.AddRange(command);
        return ProcessRunner.RunBinaryAsync(AdbPath, WithSerial(arguments), timeout, cancellationToken);
    }

    /// <summary>Liest die vollständige Ausgabe von "adb shell getprop".</summary>
    public async Task<string> GetPropAsync(CancellationToken cancellationToken = default)
    {
        var result = await RunShellAsync(new[] { "getprop" }, TimeSpan.FromSeconds(30), cancellationToken)
            .ConfigureAwait(false);
        return result.StandardOutput;
    }

    /// <summary>Listet angeschlossene Geräte (ohne Serial-Filter).</summary>
    public async Task<IReadOnlyList<AdbDevice>> GetDevicesAsync(CancellationToken cancellationToken = default)
    {
        // Bewusst ohne "-s": die Geräteliste selbst darf kein Zielgerät setzen.
        var result = await ProcessRunner.RunAsync(AdbPath, new[] { "devices", "-l" }, cancellationToken)
            .ConfigureAwait(false);
        return AdbDeviceReader.Parse(result.StandardOutput);
    }

    /// <summary>
    /// Liest die platform-tools-Version aus der Ausgabe von "adb --version".
    /// Beispielzeile: "Version 37.0.1-14910801" -> "37.0.1".
    /// </summary>
    public static string? ParseVersion(string versionText)
    {
        if (string.IsNullOrWhiteSpace(versionText))
        {
            return null;
        }

        foreach (var line in versionText.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Version ", StringComparison.Ordinal))
            {
                continue;
            }

            var token = trimmed["Version ".Length..].Trim();
            var dash = token.IndexOf('-');
            var version = dash > 0 ? token[..dash] : token;
            if (!string.IsNullOrWhiteSpace(version))
            {
                return version;
            }
        }

        return null;
    }

    /// <summary>Erzeugt eine Abbruchquelle, die nach <paramref name="limit"/> auslöst.</summary>
    private static CancellationTokenSource CreateTimeoutSource(CancellationToken cancellationToken, TimeSpan limit)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(limit);
        return source;
    }

    private IEnumerable<string> WithSerial(IEnumerable<string> arguments)
    {
        if (!string.IsNullOrWhiteSpace(Serial))
        {
            yield return "-s";
            yield return Serial;
        }

        foreach (var argument in arguments)
        {
            yield return argument;
        }
    }
}
