using System;
using System.Globalization;
using System.Reflection;
using AndroidDebloater.Core.Localization;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AndroidDebloater.App.ViewModels;

/// <summary>
/// Zustand des Hilfe-Fensters (M11): links die Navigation zwischen der
/// Anleitung (aus der gebuendelten USER-GUIDE.md) und dem About-Bereich
/// (Version, Drittlizenzen, Disclaimer, G-13).
/// </summary>
public partial class HelpViewModel : ViewModelBase
{
    /// <summary>Index der Navigation: 0 = Anleitung, 1 = About.</summary>
    [ObservableProperty]
    private int _selectedTabIndex;

    public string Title => Strings.HelpTitle;
    public string CloseButton => Strings.HelpCloseButton;

    public string UserGuideTab => Strings.HelpTabUserGuide;
    public string AboutTab => Strings.HelpTabAbout;

    /// <summary>True, wenn die Anleitung angezeigt wird.</summary>
    public bool IsUserGuideVisible => SelectedTabIndex == 0;

    /// <summary>True, wenn der About-Bereich angezeigt wird.</summary>
    public bool IsAboutVisible => SelectedTabIndex == 1;

    public string AboutName => Strings.AppTitle;
    public string AboutVersionText =>
        string.Format(CultureInfo.CurrentCulture, Strings.AboutVersionFormat, AppVersion);
    public string AboutLicenseText => Strings.AboutLicenseText;
    public string AboutComponentsLabel => Strings.AboutComponentsLabel;
    public string AboutComponentsBody => Strings.AboutComponentsBody;
    public string AboutDisclaimerText => Strings.AboutDisclaimerText;
    public string AboutWarningText => Strings.AboutWarningText;

    /// <summary>Anzeigbare Programmversion (aus der Assembly).</summary>
    public static string AppVersion
    {
        get
        {
            var assembly = typeof(HelpViewModel).Assembly;

            var informational = assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
                .InformationalVersion;

            if (!string.IsNullOrWhiteSpace(informational))
            {
                // Build-Metadaten (z. B. "+<commit>") fuer die Anzeige abschneiden.
                var plus = informational.IndexOf('+');
                return plus >= 0 ? informational[..plus] : informational;
            }

            return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        OnPropertyChanged(nameof(IsUserGuideVisible));
        OnPropertyChanged(nameof(IsAboutVisible));
    }
}
