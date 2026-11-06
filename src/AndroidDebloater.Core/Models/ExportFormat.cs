namespace AndroidDebloater.Core.Models;

/// <summary>
/// Datei-Formate des Paket-Exports (projektinfo.txt M7 / F-09).
/// </summary>
public enum ExportFormat
{
    /// <summary>Komma-getrennte Tabelle (Excel-kompatibel).</summary>
    Csv,

    /// <summary>Markdown-Tabelle für Notizen und Pull Requests.</summary>
    Markdown,

    /// <summary>Eigenständige HTML-Seite zum Ansehen und Weitergeben.</summary>
    Html,
}
