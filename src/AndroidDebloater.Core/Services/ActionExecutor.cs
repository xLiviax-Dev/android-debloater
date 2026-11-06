using System.Globalization;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Führt einen Aktionsplan am Zielgerät aus (projektinfo.txt M6.2). Ablauf:
/// Zustand merken und Wiederherstellungs-Profil sichern (S-04), Befehl
/// ausführen, bei verweigertem "uninstall" auf "disable-user" ausweichen
/// (S-12), Zustand neu einlesen (S-10) und protokollieren (S-14). Es wird nie
/// ohne gesicherten Rückweg geschrieben.
/// </summary>
public sealed class ActionExecutor
{
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromMinutes(1);

    private readonly AdbRunner _adb;

    /// <summary>Erzeugt den Ausführer für das gewählte Zielgerät.</summary>
    public ActionExecutor(AdbRunner adb)
        => _adb = adb ?? throw new ArgumentNullException(nameof(adb));

    /// <summary>Alle Änderungen dieser Sitzung – Grundlage des Ein-Klick-Rollbacks (S-06).</summary>
    public RollbackProfile Profile { get; } = new();

    /// <summary>Pfad, unter dem das Profil zuletzt gesichert wurde.</summary>
    public string? ProfilePath { get; private set; }

    /// <summary>
    /// True, wenn der Befehl verweigert wurde (Rückgabecode ungleich 0 oder
    /// "Failure" in der Ausgabe). Dann greift der Fallback (S-12).
    /// </summary>
    public static bool IsCommandRefused(ProcessResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.ExitCode != 0)
        {
            return true;
        }

        return (result.StandardOutput + "\n" + result.StandardError)
            .Contains("Failure", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>True, wenn der Zustand des Pakets zur Aktion passt (S-10).</summary>
    public static bool IsSatisfied(PackageAction action, AndroidPackage package)
    {
        ArgumentNullException.ThrowIfNull(package);

        return action switch
        {
            PackageAction.UninstallForUser => !package.Installed,
            PackageAction.UninstallPermanent => !package.Installed,
            PackageAction.UninstallKeepData => !package.Installed,
            PackageAction.PurgeRemovedData => !package.ExistsOnDevice,
            PackageAction.InstallExisting => package.Installed,
            PackageAction.DisableForUser => !package.IsEnabled,
            PackageAction.EnableForUser => package.IsEnabled,
            _ => false,
        };
    }

    /// <summary>Führt den Plan aus und liefert das Ergebnis samt Vorher-/Nachher-Zustand.</summary>
    public async Task<ActionOutcome> ExecuteAsync(
        PlannedAction plan,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var package = plan.Package;
        var timestamp = DateTimeOffset.Now;
        var wasInstalled = package.Installed;
        var previousEnabledState = package.EnabledState;
        var executed = new List<string> { plan.CommandText };

        // S-04: Rückweg VOR dem Eingriff festschreiben. Ein Absturz dazwischen
        // darf die Wiederherstellung nicht verhindern.
        var entry = new RollbackEntry
        {
            PackageName = package.PackageName,
            DisplayName = package.DisplayName,
            Action = plan.Action,
            WasInstalled = wasInstalled,
            PreviousEnabledState = previousEnabledState,
            Command = plan.Command,
            RestoreAction = plan.RestoreAction,
            RestoreCommand = plan.RestoreCommand,
            IsPermanent = plan.IsPermanent,
            Timestamp = timestamp,
        };
        Profile.Entries.Add(entry);
        ProfilePath = await SaveProfileAsync(timestamp, cancellationToken).ConfigureAwait(false);

        async Task<ActionOutcome> FinalizeAsync(
            ActionStatus status,
            string? note,
            string? output,
            PackageAction effectiveAction,
            CancellationToken token)
        {
            entry.Succeeded = status is ActionStatus.Succeeded or ActionStatus.FallbackSucceeded;
            entry.Note = note;
            ProfilePath = await SaveProfileAsync(timestamp, token).ConfigureAwait(false);

            var outcome = new ActionOutcome
            {
                Plan = plan,
                Status = status,
                EffectiveAction = effectiveAction,
                WasInstalled = wasInstalled,
                PreviousEnabledState = previousEnabledState,
                IsInstalled = package.Installed,
                CurrentEnabledState = package.EnabledState,
                Timestamp = timestamp,
                ExecutedCommands = executed.ToArray(),
                Message = note,
                CommandOutput = output,
                RollbackProfilePath = ProfilePath,
            };

            await ActionLogWriter.AppendAsync(outcome, AppPaths.ActionLogFile, token).ConfigureAwait(false);
            return outcome;
        }

        var (result, error) = await TryRunAsync(plan.Command, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return await FinalizeAsync(
                ActionStatus.Unknown,
                string.Format(CultureInfo.CurrentCulture, Strings.ActionAbortedNoteFormat, error),
                null,
                plan.Action,
                cancellationToken).ConfigureAwait(false);
        }

        var usedFallback = false;
        if (plan.Action == PackageAction.UninstallForUser && IsCommandRefused(result!))
        {
            var fallback = ActionPlanner.CreateFallbackPlan(package);
            if (fallback is not null)
            {
                usedFallback = true;
                executed.Add(fallback.CommandText);

                // Ab jetzt ist der gültige Rückweg "enable", nicht "install-existing".
                entry.Action = fallback.Action;
                entry.Command = fallback.Command;
                entry.RestoreAction = fallback.RestoreAction;
                entry.RestoreCommand = fallback.RestoreCommand;

                var (fallbackResult, fallbackError) =
                    await TryRunAsync(fallback.Command, cancellationToken).ConfigureAwait(false);
                if (fallbackError is not null)
                {
                    return await FinalizeAsync(
                        ActionStatus.Unknown,
                        string.Format(CultureInfo.CurrentCulture, Strings.ActionAbortedNoteFormat, fallbackError),
                        null,
                        PackageAction.DisableForUser,
                        cancellationToken).ConfigureAwait(false);
                }

                result = fallbackResult;
            }
        }

        var refreshed = await TryRefreshAsync(package, cancellationToken).ConfigureAwait(false);
        var effectiveAction = usedFallback ? PackageAction.DisableForUser : plan.Action;

        ActionStatus status;
        string? note;

        if (!refreshed)
        {
            status = ActionStatus.Unknown;
            note = Strings.ActionUnknownNote;
        }
        else if (IsSatisfied(effectiveAction, package))
        {
            status = usedFallback ? ActionStatus.FallbackSucceeded : ActionStatus.Succeeded;
            note = usedFallback ? Strings.ActionFallbackNote : null;
        }
        else if (IsCommandRefused(result!))
        {
            status = ActionStatus.Failed;
            note = FormatFailure(result!);
        }
        else
        {
            status = ActionStatus.Unknown;
            note = Strings.ActionUnconfirmedNote;
        }

        var output = (result!.StandardOutput + result.StandardError).Trim();
        return await FinalizeAsync(status, note, output.Length == 0 ? null : output, effectiveAction, cancellationToken)
            .ConfigureAwait(false);
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

    private async Task<string> SaveProfileAsync(DateTimeOffset timestamp, CancellationToken cancellationToken)
    {
        var path = Path.Combine(AppPaths.RollbackFolder, RollbackProfileStore.CreateFileName(timestamp));
        await RollbackProfileStore.SaveAsync(Profile, path, cancellationToken).ConfigureAwait(false);
        return path;
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
