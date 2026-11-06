using System.Xml.Linq;
using AndroidDebloater.Core.Models;

namespace AndroidDebloater.Core.Services;

/// <summary>
/// Liest Version, Dateinamen, Größen und SHA-1-Prüfsummen der platform-tools
/// aus dem offiziellen Google-SDK-Manifest (repository2-1.xml, Abschnitt 4.1).
/// Das Parsen ist bewusst namespace-unabhängig (über den lokalen Elementnamen),
/// weil das Manifest einen XML-Namespace verwendet.
/// </summary>
public static class PlatformToolsManifestReader
{
    /// <summary>Parst das Manifest-XML und liefert die platform-tools-Version.</summary>
    /// <param name="xml">Inhalt von repository2-1.xml.</param>
    /// <param name="packagePath">Paketpfad, standardmäßig "platform-tools".</param>
    public static PlatformToolsRelease Parse(
        string xml,
        string packagePath = AdbEndpoints.PlatformToolsPackagePath)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            throw new ArgumentException("Manifest ist leer.", nameof(xml));
        }

        XDocument document = XDocument.Parse(xml);

        var package = document.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "remotePackage"
                && (string?)e.Attribute("path") == packagePath)
            ?? throw new InvalidDataException($"Paket '{packagePath}' wurde im Manifest nicht gefunden.");

        var version = ReadVersion(package);
        var archives = ReadArchives(package).ToList();
        if (archives.Count == 0)
        {
            throw new InvalidDataException($"Keine Archive fuer '{packagePath}' im Manifest gefunden.");
        }

        return new PlatformToolsRelease(version, version, archives);
    }

    private static string ReadVersion(XElement package)
    {
        var revision = Child(package, "revision");
        if (revision is null)
        {
            return string.Empty;
        }

        var major = Child(revision, "major")?.Value ?? "0";
        var minor = Child(revision, "minor")?.Value ?? "0";
        var micro = Child(revision, "micro")?.Value ?? "0";
        return $"{major}.{minor}.{micro}";
    }

    private static IEnumerable<AdbReleaseArchive> ReadArchives(XElement package)
    {
        var archives = Child(package, "archives");
        if (archives is null)
        {
            yield break;
        }

        foreach (var archive in ArchivesElements(archives))
        {
            var hostOs = ParseHostOs(Child(archive, "host-os")?.Value);
            if (hostOs is null)
            {
                continue;
            }

            var complete = Child(archive, "complete");
            if (complete is null)
            {
                continue;
            }

            var fileName = Child(complete, "url")?.Value?.Trim();
            var sha1 = Child(complete, "checksum")?.Value?.Trim();
            if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(sha1))
            {
                continue;
            }

            _ = long.TryParse(Child(complete, "size")?.Value, out var size);

            yield return new AdbReleaseArchive(
                hostOs.Value,
                fileName,
                size,
                sha1.ToLowerInvariant());
        }
    }

    private static HostOperatingSystem? ParseHostOs(string? token) => token?.Trim() switch
    {
        "windows" => HostOperatingSystem.Windows,
        "macosx" => HostOperatingSystem.MacOs,
        "linux" => HostOperatingSystem.Linux,
        _ => null,
    };

    private static XElement? Child(XElement parent, string localName)
        => parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static IEnumerable<XElement> ArchivesElements(XElement parent)
        => parent.Elements().Where(e => e.Name.LocalName == "archive");
}
