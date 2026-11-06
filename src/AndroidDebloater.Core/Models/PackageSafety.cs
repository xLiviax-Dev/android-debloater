namespace AndroidDebloater.Core.Models;

/// <summary>
/// Gefahren-Einstufung eines Pakets (projektinfo.txt Abschnitt 6.2).
/// Jedes Paket bekommt genau eine Stufe; bei Unklarheit gilt <see cref="Caution"/>.
/// </summary>
public enum PackageSafety
{
    /// <summary>Unbedenklich (typische Bloatware, Spiele, Zusatz-Apps).</summary>
    Safe,

    /// <summary>Vorsicht (System-/OEM-Dienste, unklare Funktion).</summary>
    Caution,

    /// <summary>Kritisch (Abschnitt 6.1) – technisch gesperrt, nicht umgehbar.</summary>
    Critical,
}
