using Avalonia.Controls;
using Avalonia.Interactivity;

namespace AndroidDebloater.App.Views;

/// <summary>
/// Einstellungs-Dialog (projektinfo.txt M8). Die Sicherheitslogik liegt im
/// ViewModel bzw. im Core; dieses Fenster verdrahtet nur die Anzeige.
/// </summary>
public partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void OnCloseClick(object? sender, RoutedEventArgs e) => Close();
}
