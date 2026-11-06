namespace AndroidDebloater.Core.Models;

/// <summary>
/// Kopfdaten eines Exports (projektinfo.txt M7 / F-09): Zielgerät,
/// Android-Version und Zeitpunkt. Alle Werte stammen aus echten Abfragen.
/// </summary>
/// <param name="Device">Gerätebezeichnung bzw. Modell (mit Seriennummer, falls bekannt).</param>
/// <param name="AndroidVersion">Formatierte Android-Version (z. B. "Android 16 (SDK 36)"), sonst null.</param>
/// <param name="Created">Erstellungszeitpunkt des Exports.</param>
public sealed record ExportInfo(string Device, string? AndroidVersion, DateTimeOffset Created);
