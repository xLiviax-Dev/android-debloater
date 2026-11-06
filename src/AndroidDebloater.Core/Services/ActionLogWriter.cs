using System.Text;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Schreibt das Aktions-Protokoll (projektinfo.txt S-14): je Aktion eine Zeile
/// mit Zeitstempel, ausgeführtem Befehl und Vorher-/Nachher-Zustand. Die Datei
/// wird fortgeschrieben und nie überschrieben.
/// </summary>
public static class ActionLogWriter
{
    /// <summary>Formatiert einen Protokoll-Eintrag (ohne Zeilenumbruch).</summary>
    public static string FormatEntry(ActionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        var name = outcome.Plan.Package.PackageName;
        var before = DescribeState(outcome.WasInstalled, outcome.PreviousEnabledState);
        var after = DescribeState(outcome.IsInstalled, outcome.CurrentEnabledState);
        var commands = string.Join(" ; ", outcome.ExecutedCommands);
        var note = string.IsNullOrWhiteSpace(outcome.Message) ? string.Empty : $" | {outcome.Message}";

        return $"{outcome.Timestamp:yyyy-MM-dd HH:mm:ss} | {outcome.Status} | {name} | "
            + $"{outcome.EffectiveAction} | {before} -> {after} | {commands}{note}";
    }

    /// <summary>Hängt einen Eintrag an die Protokolldatei an.</summary>
    public static async Task AppendAsync(
        ActionOutcome outcome,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.AppendAllTextAsync(path, FormatEntry(outcome) + Environment.NewLine, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>Formatiert eine Rollback-Zeile (S-06) für das Protokoll.</summary>
    public static string FormatRollbackEntry(DateTimeOffset timestamp, RollbackEntryOutcome entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var note = string.IsNullOrWhiteSpace(entry.Note) ? string.Empty : $" | {entry.Note}";
        return $"{timestamp:yyyy-MM-dd HH:mm:ss} | {entry.Status} | {entry.PackageName} | "
            + $"rollback/{entry.RestoreAction} | {entry.RestoreCommandText}{note}";
    }

    /// <summary>Hängt alle Zeilen einer Rücknahme an die Protokolldatei an.</summary>
    public static async Task AppendRollbackAsync(
        RollbackOutcome outcome,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var builder = new StringBuilder();
        foreach (var entry in outcome.Entries)
        {
            builder.AppendLine(FormatRollbackEntry(outcome.Timestamp, entry));
        }

        await File.AppendAllTextAsync(path, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }

    private static string DescribeState(bool installed, int enabledState)
    {
        if (!installed)
        {
            return "removed";
        }

        // Zustand 0 (DEFAULT) und 1 (ENABLED) bedeuten aktiv; 2/3/4 deaktiviert.
        return enabledState is 0 or 1 ? "installed/active" : $"installed/disabled({enabledState})";
    }
}
