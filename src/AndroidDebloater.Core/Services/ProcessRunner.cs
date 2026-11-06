using System.Diagnostics;
using System.Text;

namespace AndroidDebloater.Core.Services;

/// <summary>Ergebnis eines externen Prozessaufrufs.</summary>
/// <param name="ExitCode">Rückgabecode des Prozesses.</param>
/// <param name="StandardOutput">Ausgabe auf stdout.</param>
/// <param name="StandardError">Ausgabe auf stderr.</param>
public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>True, wenn der Prozess mit Rückgabecode 0 endete.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>
/// Führt externe Programme aus – ohne Shell. Die Argumente werden einzeln
/// übergeben (<see cref="ProcessStartInfo.ArgumentList"/>), damit keine
/// Shell-Interpolation und damit keine Injection möglich ist (Abschnitt 8).
/// </summary>
public static class ProcessRunner
{
    /// <summary>Startet ein Programm, wartet auf das Ende und liefert die Ausgaben.</summary>
    public static async Task<ProcessResult> RunAsync(
        string executablePath,
        IEnumerable<string> arguments,
        CancellationToken cancellationToken = default)
    {
        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Beide Ströme gleichzeitig lesen, sonst kann der Puffer volllaufen (Deadlock).
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        var stdout = await stdoutTask.ConfigureAwait(false);
        var stderr = await stderrTask.ConfigureAwait(false);

        return new ProcessResult(process.ExitCode, stdout, stderr);
    }

    /// <summary>
    /// Führt ein Programm aus und liefert die Rohausgabe (stdout) als Bytes.
    /// Wird für Binärübertragungen gebraucht, z. B. <c>adb exec-out unzip -p</c>.
    /// Die Argumente werden weiterhin einzeln übergeben (keine Shell).
    /// </summary>
    public static async Task<byte[]> RunBinaryAsync(
        string executablePath,
        IEnumerable<string> arguments,
        TimeSpan? timeout,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);

        var startInfo = new ProcessStartInfo(executablePath)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout is { } limit)
        {
            timeoutSource.CancelAfter(limit);
        }

        var token = timeoutSource.Token;

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        using var buffer = new MemoryStream();

        // stdout binär mitschreiben, stderr parallel leeren (sonst kann der Puffer volllaufen).
        var copyOut = process.StandardOutput.BaseStream.CopyToAsync(buffer, token);
        var drainErr = process.StandardError.ReadToEndAsync(token);

        try
        {
            await process.WaitForExitAsync(token).ConfigureAwait(false);
            await copyOut.ConfigureAwait(false);
            await drainErr.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Beenden ist nur ein Best-Effort-Versuch.
            }

            throw;
        }

        return buffer.ToArray();
    }
}
