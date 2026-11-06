using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests des Paket-Exports (projektinfo.txt M7 / F-09): CSV, Markdown und HTML
/// inkl. Maskierung (Sonderzeichen dürfen die Formate nicht zerstören) und
/// Dateiname. Es wird kein Gerät kontaktiert.
/// </summary>
public class ExportServiceTests
{
    private static AndroidPackage Pkg(string name) => new() { PackageName = name };

    private static ExportInfo Info() => new(
        "Lenovo TB311FU (TEST1234)",
        "Android 16 (SDK 36)",
        new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.FromHours(1)));

    private static string[] Lines(string text)
        => text.TrimEnd().Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

    [Fact]
    public void CreateFileName_UsesTimestampAndFormatExtension()
    {
        var timestamp = new DateTimeOffset(2026, 11, 2, 14, 30, 55, TimeSpan.FromHours(1));

        Assert.Equal("packages-20261102-143055.csv", ExportService.CreateFileName(ExportFormat.Csv, timestamp));
        Assert.Equal("packages-20261102-143055.md", ExportService.CreateFileName(ExportFormat.Markdown, timestamp));
        Assert.Equal("packages-20261102-143055.html", ExportService.CreateFileName(ExportFormat.Html, timestamp));
    }

    [Fact]
    public void ToCsv_HasHeaderAndOneRowPerPackage()
    {
        var lines = Lines(ExportService.ToCsv(new[] { Pkg("com.a"), Pkg("com.b") }));

        Assert.Equal(3, lines.Length);
        Assert.Contains("com.a", lines[1]);
        Assert.Contains("com.b", lines[2]);
    }

    [Fact]
    public void ToCsv_StartsWithLocalizedHeaderRow()
    {
        var header = string.Join(",", new[]
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
        });

        Assert.StartsWith(header, ExportService.ToCsv(new[] { Pkg("com.a") }));
    }

    [Fact]
    public void ToCsv_QuotesCommasAndDoublesQuotes()
    {
        var package = Pkg("com.a");
        package.DisplayName = "Cool, \"App\"";

        var row = Lines(ExportService.ToCsv(new[] { package }))[1];

        Assert.Contains("\"Cool, \"\"App\"\"\"", row);
    }

    [Fact]
    public void ToMarkdown_IsTableWithHeaderSeparatorAndEscapesPipes()
    {
        var package = Pkg("com.a");
        package.DisplayName = "A|B";

        var lines = Lines(ExportService.ToMarkdown(new[] { package }, Info()));

        Assert.Contains(lines, line => line.StartsWith('|') && line.Contains("---"));
        Assert.Contains("A\\|B", ExportService.ToMarkdown(new[] { package }, Info()));
    }

    [Fact]
    public void ToHtml_EscapesMarkupInValues()
    {
        var package = Pkg("com.a");
        package.DisplayName = "<b>&Co";

        var html = ExportService.ToHtml(new[] { package }, Info());

        Assert.Contains("&lt;b&gt;&amp;Co", html);
        Assert.DoesNotContain("<b>&Co", html);
    }

    [Fact]
    public void ToHtml_MarksBloatwareRows()
    {
        var package = Pkg("com.bloat");
        package.BloatScore = BloatwareClassifier.BloatThreshold;

        Assert.Contains("class=\"bloat\"", ExportService.ToHtml(new[] { package }, Info()));
        Assert.DoesNotContain("class=\"bloat\"", ExportService.ToHtml(new[] { Pkg("com.clean") }, Info()));
    }

    [Fact]
    public void Build_SelectsRequestedFormat()
    {
        var packages = new[] { Pkg("com.a") };

        Assert.StartsWith("<!DOCTYPE html>", ExportService.Build(ExportFormat.Html, packages, Info()));
        Assert.StartsWith("# ", ExportService.Build(ExportFormat.Markdown, packages, Info()));
        Assert.StartsWith(Strings.ColumnName + ",", ExportService.Build(ExportFormat.Csv, packages, Info()));
    }

    [Fact]
    public async Task WriteAsync_CreatesFileWithContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"export-test-{Guid.NewGuid():N}.csv");

        try
        {
            await ExportService.WriteAsync(ExportFormat.Csv, new[] { Pkg("com.a") }, Info(), path);

            Assert.True(File.Exists(path));
            Assert.Contains("com.a", await File.ReadAllTextAsync(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
