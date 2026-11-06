using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidDebloater.App.ViewModels;

/// <summary>Eine wählbare Anzeigesprache (Auswahl vorbereitet, M8).</summary>
public sealed record LanguageOption(string Code, string DisplayName);

/// <summary>
/// Zustand des Einstellungs-Dialogs (projektinfo.txt M8): Sprache, Whitelist
/// (P2-5), ADB-Pfad, Icon-Cache und der Experten-Mode (S-20). Die Sicherheits-
/// logik selbst liegt im Core (<see cref="ExpertModeRules"/>, <see cref="ProtectionLists"/>).
/// </summary>
public partial class SettingsViewModel : ViewModelBase
{
    private static readonly LanguageOption EnglishLanguage = new(AppSettings.DefaultLanguage, "English");

    private readonly AppSettings _settings;

    /// <summary>Für den Designer (liest die echten Einstellungen).</summary>
    public SettingsViewModel()
        : this(SettingsStore.Load())
    {
    }

    /// <summary>Erzeugt das ViewModel aus den übergebenen Einstellungen.</summary>
    public SettingsViewModel(AppSettings settings)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        WhitelistEntries = new ObservableCollection<string>(
            settings.EffectiveWhitelist.OrderBy(entry => entry, StringComparer.Ordinal));

        SelectedLanguage = LanguageOptions.FirstOrDefault(option =>
            string.Equals(option.Code, settings.Language, StringComparison.OrdinalIgnoreCase))
            ?? EnglishLanguage;

        AdbPath = settings.AdbPath ?? string.Empty;
        IsExpertModeOn = ProtectionLists.ExpertModeEnabled;
        RefreshIconCache();
        RefreshGameList();
    }

    public string Title => Strings.SettingsTitle;
    public string SaveButton => Strings.SettingsSaveButton;
    public string CloseButton => Strings.SettingsCloseButton;
    public string CancelButton => Strings.SettingsCancelButton;

    public string LanguageLabel => Strings.SettingsLanguageLabel;
    public string LanguageHint => Strings.SettingsLanguageHint;

    /// <summary>Wählbare Sprachen; derzeit nur Englisch (Auswahl vorbereitet).</summary>
    public IReadOnlyList<LanguageOption> LanguageOptions { get; } = new[] { EnglishLanguage };

    public string WhitelistLabel => Strings.SettingsWhitelistLabel;
    public string WhitelistHint => Strings.SettingsWhitelistHint;
    public string WhitelistPlaceholder => Strings.SettingsWhitelistPlaceholder;
    public string AddWhitelistButton => Strings.SettingsWhitelistAddButton;
    public string RemoveWhitelistButton => Strings.SettingsWhitelistRemoveButton;
    public string ResetWhitelistButton => Strings.SettingsWhitelistResetButton;
    public string WhitelistEmptyNote => Strings.SettingsWhitelistEmptyNote;

    public string AdbPathLabel => Strings.SettingsAdbPathLabel;
    public string AdbPathHint => Strings.SettingsAdbPathHint;
    public string AdbPathAutomaticButton => Strings.SettingsAdbPathAutomaticButton;

    public string IconCacheLabel => Strings.SettingsIconCacheLabel;
    public string ClearIconCacheButton => Strings.SettingsClearIconCacheButton;
    public string IconCacheEmpty => Strings.SettingsIconCacheEmpty;

    public string GameListLabel => Strings.SettingsGameListLabel;
    public string GameListEmpty => Strings.SettingsGameListEmpty;
    public string ClearGameListButton => Strings.SettingsClearGameListButton;

    public string ExpertLabel => Strings.SettingsExpertLabel;
    public string ExpertOpenButton => Strings.SettingsExpertOpenButton;
    public string ExpertContinueButton => Strings.SettingsExpertContinueButton;
    public string ExpertEnableButton => Strings.SettingsExpertEnableButton;
    public string ExpertDisableButton => Strings.SettingsExpertDisableButton;
    public string ExpertStep1Text => Strings.SettingsExpertStep1Text;
    public string ExpertStep2Text => Strings.SettingsExpertStep2Text;
    public string ExpertAckText => Strings.SettingsExpertAck;

    [ObservableProperty]
    private LanguageOption _selectedLanguage = EnglishLanguage;

    [ObservableProperty]
    private ObservableCollection<string> _whitelistEntries = new();

    [ObservableProperty]
    private string? _selectedWhitelistEntry;

    [ObservableProperty]
    private string _newWhitelistEntry = string.Empty;

    [ObservableProperty]
    private string _adbPath = string.Empty;

    [ObservableProperty]
    private string _iconCacheUsageText = string.Empty;

    [ObservableProperty]
    private string _gameListUsageText = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    /// <summary>True, wenn der Experten-Mode gerade aktiv ist (S-20).</summary>
    [ObservableProperty]
    private bool _isExpertModeOn;

    /// <summary>Aktueller Schritt im Freischalt-Ablauf (0 = aus, 1/2 = Bestätigungen).</summary>
    [ObservableProperty]
    private int _expertStep = ExpertModeRules.StepOff;

    /// <summary>Zweite Bestätigung: Folgen ausdrücklich anerkannt (S-20a).</summary>
    [ObservableProperty]
    private bool _expertAck;

    /// <summary>True, wenn die Whitelist Einträge hat.</summary>
    public bool HasWhitelistEntries => WhitelistEntries.Count > 0;

    /// <summary>True, wenn der Einschalt-Knopf gezeigt wird (aus und noch keine Warnung).</summary>
    public bool CanShowExpertOpen => !IsExpertModeOn && ExpertStep == ExpertModeRules.StepOff;

    /// <summary>True, wenn die erste Warnung mit Bestätigung angezeigt wird (S-20a).</summary>
    public bool IsExpertStep1Visible
        => !IsExpertModeOn && ExpertStep == ExpertModeRules.StepWarningAcknowledged;

    /// <summary>True, wenn die zweite, getrennte Bestätigung angezeigt wird (S-20a).</summary>
    public bool IsExpertStep2Visible
        => !IsExpertModeOn && ExpertStep == ExpertModeRules.StepConsequencesAcknowledged;

    /// <summary>Statuszeile des Experten-Modes (ein/aus).</summary>
    public string ExpertStateText => IsExpertModeOn
        ? Strings.SettingsExpertOnText
        : Strings.SettingsExpertOffText;

    /// <summary>Übernimmt Whitelist, Sprache und ADB-Pfad, wendet sie an und speichert.</summary>
    [RelayCommand]
    public async Task SaveAsync()
    {
        _settings.Whitelist = WhitelistEntries.ToList();
        _settings.AdbPath = string.IsNullOrWhiteSpace(AdbPath) ? null : AdbPath.Trim();
        _settings.Language = SelectedLanguage.Code;

        SettingsStore.Apply(_settings);
        await SaveSettingsFileAsync().ConfigureAwait(true);
        Status = Strings.SettingsSavedStatus;
    }

    [RelayCommand]
    private void AddWhitelistEntry()
    {
        var entry = NewWhitelistEntry?.Trim();
        if (string.IsNullOrEmpty(entry))
        {
            return;
        }

        if (!WhitelistEntries.Contains(entry, StringComparer.Ordinal))
        {
            WhitelistEntries.Add(entry);
            OnPropertyChanged(nameof(HasWhitelistEntries));
        }

        NewWhitelistEntry = string.Empty;
    }

    [RelayCommand]
    private void RemoveWhitelistEntry()
    {
        if (SelectedWhitelistEntry is not { } entry)
        {
            return;
        }

        WhitelistEntries.Remove(entry);
        OnPropertyChanged(nameof(HasWhitelistEntries));
    }

    [RelayCommand]
    private void RestoreDefaultWhitelist()
    {
        WhitelistEntries.Clear();
        foreach (var entry in ProtectionLists.DefaultWhitelist.OrderBy(value => value, StringComparer.Ordinal))
        {
            WhitelistEntries.Add(entry);
        }

        OnPropertyChanged(nameof(HasWhitelistEntries));
    }

    [RelayCommand]
    private void ResetAdbPath() => AdbPath = string.Empty;

    [RelayCommand]
    private void RefreshIconCache()
    {
        var usage = IconCache.GetUsage();
        IconCacheUsageText = usage.FileCount == 0
            ? Strings.SettingsIconCacheEmpty
            : string.Format(
                CultureInfo.CurrentCulture,
                Strings.SettingsIconCacheUsageFormat,
                usage.FileCount,
                Humanize.Bytes(usage.TotalBytes));
    }

    /// <summary>Aktualisiert die Anzeige der gemerkten Spiele-Anzahl.</summary>
    private void RefreshGameList()
    {
        var count = GameRegistry.Load().Count;
        GameListUsageText = count == 0
            ? Strings.SettingsGameListEmpty
            : string.Format(CultureInfo.CurrentCulture, Strings.SettingsGameListUsageFormat, count);
    }

    [RelayCommand]
    private void ClearIconCache()
    {
        var removed = IconCache.Clear();

        RefreshIconCache();
        Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusIconCacheClearedFormat, removed);
    }

    /// <summary>
    /// Setzt die gemerkte Spiele-Liste zurück. Bewusst getrennt vom Cache-Leeren:
    /// Ein entferntes Spiel kann das Gerät nicht mehr als Spiel melden, die Liste
    /// wäre also sonst dauerhaft neu aufgebaut.
    /// </summary>
    [RelayCommand]
    private void ClearGameList()
    {
        var removed = GameRegistry.Load().Count;
        GameRegistry.Clear();

        RefreshGameList();
        Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusGameListClearedFormat, removed);
    }

    /// <summary>Erster Schritt: Warnung zeigen (erste Bestätigung durch Fortfahren).</summary>
    [RelayCommand]
    private void OpenExpertSetup()
    {
        ExpertStep = ExpertModeRules.Advance(ExpertStep);
        NotifyExpertState();
    }

    /// <summary>Zweiter Schritt: getrennte Bestätigung anfordern (S-20a).</summary>
    [RelayCommand]
    private void ContinueExpertSetup()
    {
        ExpertStep = ExpertModeRules.Advance(ExpertStep);
        NotifyExpertState();
    }

    /// <summary>Bricht den Freischalt-Ablauf ab; die Sperre bleibt bestehen.</summary>
    [RelayCommand]
    private void CancelExpertSetup()
    {
        ExpertStep = ExpertModeRules.StepOff;
        ExpertAck = false;
        NotifyExpertState();
    }

    /// <summary>Schaltet den Experten-Mode ein – erst nach beiden Bestätigungen (S-20a).</summary>
    [RelayCommand(CanExecute = nameof(CanEnableExpertMode))]
    private async Task EnableExpertModeAsync()
    {
        ProtectionLists.SetExpertMode(true);
        _settings.ExpertMode = true;
        await SaveSettingsFileAsync().ConfigureAwait(true);

        IsExpertModeOn = true;
        ExpertStep = ExpertModeRules.StepOff;
        ExpertAck = false;
        Status = Strings.StatusExpertModeEnabled;
        NotifyExpertState();
    }

    /// <summary>Schaltet den Experten-Mode aus – die harte Sperre gilt sofort wieder (S-20d).</summary>
    [RelayCommand]
    private async Task DisableExpertModeAsync()
    {
        ProtectionLists.SetExpertMode(false);
        _settings.ExpertMode = false;
        await SaveSettingsFileAsync().ConfigureAwait(true);

        IsExpertModeOn = false;
        Status = Strings.StatusExpertModeDisabled;
        NotifyExpertState();
    }

    private bool CanEnableExpertMode() => ExpertModeRules.CanEnable(ExpertStep, ExpertAck);

    private async Task SaveSettingsFileAsync()
    {
        try
        {
            await SettingsStore.SaveAsync(_settings).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
        }
    }

    private void NotifyExpertState()
    {
        OnPropertyChanged(nameof(CanShowExpertOpen));
        OnPropertyChanged(nameof(IsExpertStep1Visible));
        OnPropertyChanged(nameof(IsExpertStep2Visible));
        OnPropertyChanged(nameof(ExpertStateText));
        EnableExpertModeCommand.NotifyCanExecuteChanged();
        DisableExpertModeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsExpertModeOnChanged(bool value) => NotifyExpertState();

    partial void OnExpertStepChanged(int value) => NotifyExpertState();

    partial void OnExpertAckChanged(bool value) => NotifyExpertState();
}
