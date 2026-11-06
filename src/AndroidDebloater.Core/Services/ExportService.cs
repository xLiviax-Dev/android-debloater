using System.Globalization;
using System.Text;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Erzeugt den Paket-Export (projektinfo.txt M7 / F-09) als CSV, Markdown oder
/// eigenständige HTML-Seite. Exportiert werden genau die übergebenen Pakete –
/// in der Regel die aktuell sichtbare (gefilterte) Liste. Es wird nichts geraten;
/// alle Angaben stammen aus <see cref="AndroidPackage"/>.
/// </summary>
public static class ExportService
{
    /// <summary>
    /// Spaltentitel in der Reihenfolge der Zellen (aus der Lokalisierung, damit
    /// die Datei zur Anzeigesprache passt).
    /// </summary>
    private static string[] Headers => new[]
    {
        Strings.ColumnName,
        Strings.ColumnPackage,
        Strings.ColumnKind,
        Strings.ColumnStatus,
        Strings.ColumnSafety,
        Strings.ColumnBloat,
        Strings.ColumnSize,
        Strings.ColumnInstalled,
        Strings.ColumnInstaller,
        Strings.ColumnVersion,
    };

    /// <summary>Dateiname eines Exports, z. B. "packages-20261102-143055.csv".</summary>
    public static string CreateFileName(ExportFormat format, DateTimeOffset timestamp)
        => $"packages-{timestamp:yyyyMMdd-HHmmss}.{Extension(format)}";

    /// <summary>Dateiendung des Formats.</summary>
    public static string Extension(ExportFormat format) => format switch
    {
        ExportFormat.Markdown => "md",
        ExportFormat.Html => "html",
        _ => "csv",
    };

    /// <summary>Erzeugt den Inhalt im gewünschten Format.</summary>
    public static string Build(ExportFormat format, IReadOnlyList<AndroidPackage> packages, ExportInfo info)
        => format switch
        {
            ExportFormat.Markdown => ToMarkdown(packages, info),
            ExportFormat.Html => ToHtml(packages, info),
            _ => ToCsv(packages),
        };

    /// <summary>CSV: Kopfzeile + eine Zeile je Paket (Excel-kompatibel).</summary>
    public static string ToCsv(IReadOnlyList<AndroidPackage> packages)
    {
        ArgumentNullException.ThrowIfNull(packages);

        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", Headers.Select(EscapeCsv)));
        foreach (var package in packages)
        {
            builder.AppendLine(string.Join(",", Cells(package).Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    /// <summary>Markdown: Überschrift + Kopfdaten + Tabelle.</summary>
    public static string ToMarkdown(IReadOnlyList<AndroidPackage> packages, ExportInfo info)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(info);

        var headers = Headers;
        var builder = new StringBuilder();
        builder.Append("# ").Append(Strings.AppTitle).Append(" - ").Append(Strings.ExportTitle).AppendLine();
        builder.AppendLine();
        builder.AppendLine(MetaLine(info, packages));
        builder.AppendLine();
        builder.Append('|').AppendJoin(" | ", headers).AppendLine("|");
        builder.Append('|').AppendJoin(" | ", headers.Select(_ => "---")).AppendLine("|");
        foreach (var package in packages)
        {
            builder.Append('|').AppendJoin(" | ", Cells(package).Select(EscapeMarkdown)).AppendLine("|");
        }

        return builder.ToString();
    }

    /// <summary>HTML: eigenständige Seite; Bloatware-Zeilen sind hervorgehoben.</summary>
    public static string ToHtml(IReadOnlyList<AndroidPackage> packages, ExportInfo info)
    {
        ArgumentNullException.ThrowIfNull(packages);
        ArgumentNullException.ThrowIfNull(info);

        var headers = Headers;
        var title = $"{Strings.AppTitle} - {Strings.ExportTitle}";
        var builder = new StringBuilder();
        builder.AppendLine("<!DOCTYPE html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("<meta charset=\"utf-8\">");
        builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.Append("<title>").Append(EscapeHtml(title)).AppendLine("</title>");
        builder.AppendLine("<style>");
        builder.AppendLine("  body{font-family:'Segoe UI',system-ui,sans-serif;margin:24px;color:#1c1e21;background:#fafbfc}");
        builder.AppendLine("  h1{font-size:20px;margin:0 0 4px}");
        builder.AppendLine("  .meta{color:#666;font-size:13px;margin-bottom:16px}");
        builder.AppendLine("  table{border-collapse:collapse;width:100%;background:#fff;font-size:13px}");
        builder.AppendLine("  th,td{border:1px solid #e0e4e8;padding:6px 8px;text-align:left;vertical-align:top}");
        builder.AppendLine("  th{background:#f2f4f7;position:sticky;top:0}");
        builder.AppendLine("  tr:nth-child(even) td{background:#fafbfc}");
        builder.AppendLine("  tr.bloat td{background:#fff6e5}");
        builder.AppendLine("  .num{text-align:right}");
        builder.AppendLine("</style>");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.Append("<h1>").Append(EscapeHtml(title)).AppendLine("</h1>");
        builder.Append("<div class=\"meta\">").Append(EscapeHtml(MetaLine(info, packages))).AppendLine("</div>");
        builder.AppendLine("<table>");
        builder.Append("<tr>").AppendJoin("", headers.Select(h => $"<th>{EscapeHtml(h)}</th>")).AppendLine("</tr>");

        foreach (var package in packages)
        {
            var cells = Cells(package);
            builder.Append(package.IsBloatCandidate ? "<tr class=\"bloat\">" : "<tr>");
            for (int index = 0; index < cells.Length; index++)
            {
                var css = index is 5 or 6 ? " class=\"num\"" : string.Empty;
                builder.Append($"<td{css}>").Append(EscapeHtml(cells[index])).Append("</td>");
            }

            builder.AppendLine("</tr>");
        }

        builder.AppendLine("</table>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString();
    }

    /// <summary>
    /// Schreibt den Export als UTF-8-Datei mit BOM (damit Tabellenkalkulationen
    /// Umlaute korrekt lesen). Der Zielordner wird bei Bedarf angelegt.
    /// </summary>
    public static async Task WriteAsync(
        ExportFormat format,
        IReadOnlyList<AndroidPackage> packages,
        ExportInfo info,
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var text = Build(format, packages, info);

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(
            path,
            text,
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: true),
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Kopfzeile: Gerät · Android-Version · Zeitpunkt · Anzahl/Schätzung.</summary>
    private static string MetaLine(ExportInfo info, IReadOnlyList<AndroidPackage> packages)
    {
        var parts = new List<string>(4)
        {
            string.Format(CultureInfo.CurrentCulture, Strings.ExportDeviceFormat, info.Device),
        };

        if (!string.IsNullOrWhiteSpace(info.AndroidVersion))
        {
            parts.Add(info.AndroidVersion!);
        }

        parts.Add(string.Format(
            CultureInfo.CurrentCulture,
            Strings.ExportCreatedFormat,
            info.Created.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture)));

        parts.Add(string.Format(
            CultureInfo.CurrentCulture,
            Strings.ExportCountFormat,
            packages.Count,
            packages.Count(package => package.IsBloatCandidate)));

        return string.Join(" · ", parts);
    }

    private static string[] Cells(AndroidPackage package) => new[]
    {
        package.DisplayText,
        package.PackageName,
        package.KindText,
        package.StatusText,
        package.SafetyText,
        package.BloatText,
        package.SizeText,
        package.InstalledText,
        package.InstallerText,
        package.VersionText,
    };

    private static string EscapeCsv(string value)
        => value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private static string EscapeMarkdown(string value)
        => value.Replace("|", "\\|").Replace("\n", " ");

    private static string EscapeHtml(string value)
        => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
