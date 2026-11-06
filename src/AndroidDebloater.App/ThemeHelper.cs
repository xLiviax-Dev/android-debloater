using Avalonia;
using Avalonia.Styling;

namespace AndroidDebloater.App;

/// <summary>
/// Wendet das gespeicherte Farbschema an und schaltet zwischen hell und dunkel um.
/// "Light" / "Dark" erzwingen das Schema, null folgt dem System.
/// </summary>
public static class ThemeHelper
{
    public const string Light = "Light";
    public const string Dark = "Dark";

    /// <summary>Setzt das Schema der Anwendung; unbekannte Werte folgen dem System.</summary>
    public static void Apply(string? theme)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.RequestedThemeVariant = theme switch
        {
            Light => ThemeVariant.Light,
            Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }

    /// <summary>
    /// Wechselt von dem gerade sichtbaren Schema zum anderen und liefert den
    /// neuen Wert zum Speichern.
    /// </summary>
    public static string Toggle()
    {
        var isDark = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        var next = isDark ? Light : Dark;
        Apply(next);
        return next;
    }
}
