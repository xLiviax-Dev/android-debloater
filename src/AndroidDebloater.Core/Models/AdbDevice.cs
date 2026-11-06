namespace AndroidDebloater.Core.Models;

/// <summary>Ein von "adb devices -l" gemeldetes Gerät.</summary>
/// <param name="Serial">Seriennummer (ADB-Serial).</param>
/// <param name="State">ADB-Zustand, z. B. "device", "unauthorized", "offline".</param>
/// <param name="Model">Modellbezeichnung aus der Geräteliste, falls vorhanden.</param>
/// <param name="Product">Produktname aus der Geräteliste, falls vorhanden.</param>
/// <param name="Device">Gerätename aus der Geräteliste, falls vorhanden.</param>
public sealed record AdbDevice(
    string Serial,
    string State,
    string? Model,
    string? Product,
    string? Device)
{
    /// <summary>True, wenn das Gerät einsatzbereit ist (Zustand "device").</summary>
    public bool IsOnline => string.Equals(State, "device", StringComparison.OrdinalIgnoreCase);

    /// <summary>True, wenn die USB-Debugging-Freigabe am Gerät noch fehlt.</summary>
    public bool IsUnauthorized =>
        string.Equals(State, "unauthorized", StringComparison.OrdinalIgnoreCase);

    /// <summary>True, wenn das Gerät als offline gemeldet wird.</summary>
    public bool IsOffline => string.Equals(State, "offline", StringComparison.OrdinalIgnoreCase);

    /// <summary>Lesbare Bezeichnung für die Geräteauswahl.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Model) ? Serial : $"{Model} ({Serial})";
}
