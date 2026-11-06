using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using AndroidDebloater.App.ViewModels;
using AndroidDebloater.App.Views;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Gespeicherte Einstellungen laden und anwenden (Whitelist, Experten-Mode, M8).
            var settings = SettingsStore.Load();
            SettingsStore.Apply(settings);
            ThemeHelper.Apply(settings.Theme);

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainViewModel(new AdbInstaller(), settings),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}