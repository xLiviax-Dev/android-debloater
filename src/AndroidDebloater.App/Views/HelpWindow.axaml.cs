using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AndroidDebloater.App.Views;

/// <summary>
/// Hilfe-Fenster (M11): zeigt die gebuendelte Anleitung (USER-GUIDE.md) und
/// den About-Bereich (G-13). Dieses Fenster verdrahtet nur die Anzeige.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
