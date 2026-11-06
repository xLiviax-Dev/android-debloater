namespace AndroidDebloater.Core.Models;

/// <summary>Statische Eckdaten eines Geräts (aus "getprop").</summary>
/// <param name="Serial">ADB-Seriennummer.</param>
/// <param name="Manufacturer">Hersteller, z. B. "LENOVO".</param>
/// <param name="Model">Modell, z. B. "TB311FU".</param>
/// <param name="AndroidVersion">Android-Version, z. B. "16".</param>
/// <param name="SdkLevel">API-Level (z. B. 36), falls ermittelbar.</param>
/// <param name="BuildId">Build-Kennung, z. B. "BP2A.250605.031.A3".</param>
public sealed record DeviceInfo(
    string Serial,
    string? Manufacturer,
    string? Model,
    string? AndroidVersion,
    int? SdkLevel,
    string? BuildId);
