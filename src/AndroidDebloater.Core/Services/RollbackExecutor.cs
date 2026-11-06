using System.Globalization;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Führt den Ein-Klick-Rollback aus (projektinfo.txt M6.5 / S-06): nimmt die
/// im Wiederherstellungs-Profil gesicherten Änderungen in umgekehrter
/// Reihenfolge zurück, liest nach jeder Rücknahme den Zustand neu ein (S-10)
/// und schreibt das Ergebnis ins Protokoll (S-14). Die Rückwege sind
/// idempotent, deshalb darf direkt geschrieben werden - geprüft wird danach.
/// </summary>
public sealed class RollbackExecutor
{
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromMinutes(1);

    private readonly AdbRunner _adb;

    /// <summary>Erzeugt den Rollback-Ausführer für das gewählte Zielgerät.</summary>
    public RollbackExecutor(AdbRunner adb)
        => _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>
    /// Reihenfolge der Rücknahmen: umgekehrt zur Ausführung, damit zuletzt
    /// Geändertes zuerst zurückgenommen wird.
    /// </summary>
    public static IReadOnlyList<RollbackEntry> PlanRestores(RollbackProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var entries = new List<RollbackEntry>(profile.Entries);
        entries.Reverse();
        return entries;
    }

    /// <summary>
    /// Bestätigter Zustand nach einer Rücknahme (S-10/S-19): stimmt der frisch
    /// gelesene Zustand zum Rückweg, gilt sie als erfolgreich; sonst wird eine
    /// verweigerte Ausführung als Fehler und alles Übrige als "unbestätigt"
    /// gemeldet - nie blind "ok".
    /// </summary>
    public static ActionStatus AssessStatus(
        ProcessResult result,
        bool refreshed,
        PackageAction restoreAction,
        AndroidPackage package)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(package);

        if (refreshed && ActionExecutor.IsSatisfied(restoreAction, package))
        {
            return ActionStatus.Succeeded;
        }

        return ActionExecutor.IsCommandRefused(result) ? ActionStatus.Failed : ActionStatus.Unknown;
    }

    /// <summary>
    /// Nimmt alle Änderungen des Profils zurück und liefert das Ergebnis.
    /// Erfolgreich Wiederhergestelltes wird aus dem Profil entfernt, damit ein
    /// zweiter Klick nichts wiederholt.
    /// </summary>
    public async Task<RollbackOutcome> ExecuteAsync(
        RollbackProfile profile,
        IReadOnlyList<AndroidPackage> knownPackages,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(knownPackages);

        var timestamp = DateTimeOffset.Now;

        // Zuordnung nach Paketname, damit der Zustand am bekannten Objekt
        // aktualisiert wird und die Anzeige die Änderung mitbekommt (S-10).
        var byName = new Dictionary<string, AndroidPackage>(StringComparer.Ordinal);
        foreach (var package in knownPackages)
        {
            byName[package.PackageName] = package;
        }

        var results = new List<RollbackEntryOutcome>(profile.Entries.Count);

        // Einträge, die danach nicht mehr im Profil stehen: erfolgreich
        // wiederhergestellte und endgültige (nicht wiederherstellbare).
        var resolved = new List<RollbackEntry>();

        foreach (var entry in PlanRestores(profile))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (entry.IsPermanent)
            {
                // S-21: endgültig entfernt – kein Befehl, nur ein ehrlicher Hinweis.
                results.Add(new RollbackEntryOutcome(
                    entry.PackageName,
                    entry.DisplayName,
                    entry.RestoreAction,
                    entry.RestoreCommandText,
                    ActionStatus.NotRestorable,
                    Strings.RollbackNotRestorableNote));
                resolved.Add(entry);
                continue;
            }

            if (!byName.TryGetValue(entry.PackageName, out var package))
            {
                // Das Paket steht nicht in der aktuellen Liste; ein leerer
                // Platzhalter genügt, weil der Zustand frisch gelesen wird.
                package = new AndroidPackage { PackageName = entry.PackageName };
            }

            var (result, error) = await TryRunAsync(entry.RestoreCommand, cancellationToken).ConfigureAwait(false);
            if (error is not null)
            {
                results.Add(new RollbackEntryOutcome(
                    entry.PackageName,
                    entry.DisplayName,
                    entry.RestoreAction,
                    entry.RestoreCommandText,
                    ActionStatus.Unknown,
                    string.Format(CultureInfo.CurrentCulture, Strings.ActionAbortedNoteFormat, error)));
                continue;
            }

            var refreshed = await TryRefreshAsync(package, cancellationToken).ConfigureAwait(false);
            var status = AssessStatus(result!, refreshed, entry.RestoreAction, package);

            string? note = status switch
            {
                ActionStatus.Succeeded => null,
                ActionStatus.Failed => FormatFailure(result!),
                _ => refreshed ? Strings.ActionUnconfirmedNote : Strings.ActionUnknownNote,
            };

            results.Add(new RollbackEntryOutcome(
                entry.PackageName,
                entry.DisplayName,
                entry.RestoreAction,
                entry.RestoreCommandText,
                status,
                note));

            if (status == ActionStatus.Succeeded)
            {
                resolved.Add(entry);
            }
        }

        foreach (var entry in resolved)
        {
            profile.Entries.Remove(entry);
        }

        var outcome = new RollbackOutcome(profile.Device, timestamp, results);
        await ActionLogWriter.AppendRollbackAsync(outcome, AppPaths.ActionLogFile, cancellationToken)
            .ConfigureAwait(false);
        return outcome;
    }

    private async Task<(ProcessResult? Result, string? Error)> TryRunAsync(
        IReadOnlyList<string> command,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _adb.RunShellAsync(command, ActionTimeout, cancellationToken).ConfigureAwait(false);
            return (result, null);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return (null, exception.Message);
        }
    }

    private async Task<bool> TryRefreshAsync(AndroidPackage package, CancellationToken cancellationToken)
    {
        try
        {
            await PackageScanner.RefreshStateAsync(_adb, package, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    private static string FormatFailure(ProcessResult result)
    {
        var text = (result.StandardOutput + " " + result.StandardError).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (text.Length == 0)
        {
            text = $"exit code {result.ExitCode}";
        }

        return string.Format(CultureInfo.CurrentCulture, Strings.ActionFailedNoteFormat, text);
    }
}
