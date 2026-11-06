using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AndroidDebloater.App.Views;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AndroidDebloater.App.ViewModels;

/// <summary>
/// Zustand der Startansicht: ADB-Status prüfen, Gerät auswählen und den
/// Paketbestand des Geräts mit Suche und Filter anzeigen.
/// </summary>
public partial class MainViewModel : ViewModelBase
{
    private readonly AdbInstaller _installer;

    /// <summary>Nutzer-Einstellungen der Sitzung (M8); dieselbe Instanz nutzt der Einstellungs-Dialog.</summary>
    private readonly AppSettings _settings;

    private CancellationTokenSource? _iconPreloadCts;
    private int _busyCount;

    /// <summary>Ausführer der laufenden Sitzung (sammelt alle Änderungen für den Rückweg, S-04/S-06).</summary>
    private ActionExecutor? _executor;
    private string? _executorSerial;

    /// <summary>Für den Designer (keine echten ADB-Aufrufe).</summary>
    public MainViewModel()
        : this(new AdbInstaller())
    {
    }

    /// <summary>Erzeugt das ViewModel und startet die ADB-Prüfung.</summary>
    public MainViewModel(AdbInstaller installer, AppSettings? settings = null)
    {
        _installer = installer ?? throw new ArgumentNullException(nameof(installer));
        _settings = settings ?? SettingsStore.Load();

        if (!Design.IsDesignMode)
        {
            _ = CheckAdbAsync();
        }
    }

    public string Title => Strings.AppTitle;
    public string Hint => Strings.ShellHint;

    public string SettingsButton => Strings.SettingsButton;

    /// <summary>Text/Tooltip des Hilfe-Knopfes im Kopf (M11).</summary>
    public string HelpButton => Strings.HelpButton;

    /// <summary>Tooltip des Hell/Dunkel-Umschalters im Kopf.</summary>
    public string ThemeToggleTip => Strings.ThemeToggleTip;

    /// <summary>Untertitel und Status-Chips im Kopf der Hauptansicht.</summary>
    public string AppSubtitle => Strings.AppSubtitle;

    public string ChipAdbReady => Strings.ChipAdbReady;
    public string ChipAdbMissing => Strings.ChipAdbMissing;
    public string ChipNoDevice => Strings.ChipNoDevice;

    /// <summary>Wechselt zwischen hellem und dunklem Farbschema und speichert die Wahl.</summary>
    [RelayCommand]
    private void ToggleTheme()
    {
        _settings.Theme = ThemeHelper.Toggle();
        _ = SettingsStore.SaveAsync(_settings);
    }

    /// <summary>True, wenn der Experten-Mode aktiv ist; dann erscheint das Warnbanner (S-20b).</summary>
    public bool IsExpertModeActive => ProtectionLists.ExpertModeEnabled;

    /// <summary>Warntext des Banners, solange der Experten-Mode aktiv ist.</summary>
    public string ExpertModeBannerText => Strings.ExpertModeBannerText;

    public string AdbCardTitle => Strings.AdbCardTitle;
    public string AdbCheckButton => Strings.AdbCheckButton;
    public string AdbDownloadButton => Strings.AdbDownloadButton;
    public string AdbAssistantBody => Strings.AdbAssistantBody;
    public string ConfirmTitle => Strings.AdbConfirmTitle;
    public string ConfirmNote => Strings.AdbConfirmNote;
    public string ConfirmYes => Strings.AdbConfirmYes;
    public string ConfirmCancel => Strings.AdbConfirmCancel;

    public string ActionConfirmTitle => Strings.ActionConfirmTitle;

    /// <summary>
    /// Fußnote des Bestätigungsdialogs: normaler Hinweis auf den Rückweg bzw.
    /// bei einer endgültigen Aktion (S-21) der Hinweis, dass nur eine
    /// Neuinstallation über den Play Store möglich ist.
    /// </summary>
    public string ActionConfirmNote => PendingAction is { IsPermanent: true }
        ? Strings.ActionConfirmPermanentNote
        : Strings.ActionConfirmNote;

    public string ActionConfirmYes => Strings.ActionConfirmYes;
    public string ActionConfirmCancel => Strings.ActionConfirmCancel;
    public string ActionConfirmTypeLabel => Strings.ActionConfirmTypeLabel;

    public string RollbackButton => Strings.RollbackButton;
    public string RollbackNote => Strings.RollbackNote;
    public string RollbackOpenFolderButton => Strings.RollbackOpenFolderButton;
    public string RollbackLogTitle => Strings.RollbackLogTitle;
    public string RollbackRescueButton => Strings.RollbackRescueButton;

    public string ExportButton => Strings.ExportButton;
    public string ExportCsvOption => Strings.ExportCsvOption;
    public string ExportMarkdownOption => Strings.ExportMarkdownOption;
    public string ExportHtmlOption => Strings.ExportHtmlOption;

    /// <summary>Beschriftung der Log-Schaltfläche, abhängig von der Sichtbarkeit.</summary>
    public string RollbackLogButton => IsRollbackLogVisible
        ? Strings.RollbackHideLogButton
        : Strings.RollbackLogButton;

    public string DeviceCardTitle => Strings.DeviceCardTitle;
    public string DeviceRefreshButton => Strings.DeviceRefreshButton;
    public string DeviceNoneConnected => Strings.DeviceNoneConnected;

    public string PackageCardTitle => Strings.PackageCardTitle;
    public string PackageReloadButton => Strings.PackageReloadButton;
    public string PackageSearchPlaceholder => Strings.PackageSearchPlaceholder;

    /// <summary>Nummer des Tabs "Removed" (entfernte Pakete; Apps und Spiele gemeinsam).</summary>
    private const int RemovedTabIndex = 5;

    /// <summary>
    /// Beschriftungen der Filterauswahl des aktuellen Tabs. Im Tab "Removed" fehlen
    /// "Apps" und "Games", damit dort Apps und Spiele gemeinsam angezeigt werden.
    /// </summary>
    [ObservableProperty]
    private IReadOnlyList<string> _filterOptions = FilterLabelsFor(FilterKindsForTab(0));

    /// <summary>Filterarten, zu denen die Indizes in <see cref="FilterOptions"/> gehören.</summary>
    private IReadOnlyList<PackageFilterKind> _filterKinds = FilterKindsForTab(0);

    /// <summary>Bedeutung eines Filtereintrags, unabhängig von seiner Position.</summary>
    private enum PackageFilterKind
    {
        All,
        Apps,
        Games,
        Bloatware,
        Privileged,
    }

    /// <summary>Filterarten des Tabs; "Removed" bietet nur "All" (Apps und Spiele gemeinsam).</summary>
    private static IReadOnlyList<PackageFilterKind> FilterKindsForTab(int tabIndex) =>
        tabIndex == RemovedTabIndex
            ? new[] { PackageFilterKind.All }
            : new[]
            {
                PackageFilterKind.All,
                PackageFilterKind.Apps,
                PackageFilterKind.Games,
                PackageFilterKind.Bloatware,
                PackageFilterKind.Privileged,
            };

    /// <summary>Beschriftungen zu den Filterarten (öffentliche Strings, englisch).</summary>
    private static IReadOnlyList<string> FilterLabelsFor(IReadOnlyList<PackageFilterKind> kinds)
        => kinds.Select(FilterLabel).ToArray();

    private static string FilterLabel(PackageFilterKind kind) => kind switch
    {
        PackageFilterKind.Apps => Strings.PackageFilterApps,
        PackageFilterKind.Games => Strings.PackageFilterGames,
        PackageFilterKind.Bloatware => Strings.PackageFilterBloatware,
        PackageFilterKind.Privileged => Strings.PackageFilterPrivileged,
        _ => Strings.PackageFilterAll,
    };

    [ObservableProperty]
    private AdbStatus _adbState = AdbStatus.NotFound;

    [ObservableProperty]
    private string _status = Strings.StatusReady;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckAdbCommand))]
    [NotifyCanExecuteChangedFor(nameof(StartDownloadCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmInstallCommand))]
    [NotifyCanExecuteChangedFor(nameof(ScanPackagesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ConfirmActionCommand))]
    [NotifyCanExecuteChangedFor(nameof(ActCommand))]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(OpenHelpCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isConfirmVisible;

    [ObservableProperty]
    private PlatformToolsRelease? _pendingRelease;

    [ObservableProperty]
    private AdbReleaseArchive? _pendingArchive;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private ObservableCollection<AdbDevice> _devices = new();

    [ObservableProperty]
    private AdbDevice? _selectedDevice;

    [ObservableProperty]
    private DeviceInfo? _deviceInfo;

    [ObservableProperty]
    private ObservableCollection<AndroidPackage> _packages = new();

    [ObservableProperty]
    private ObservableCollection<AndroidPackage> _filteredPackages = new();

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _selectedFilterIndex;

    [ObservableProperty]
    private int _selectedTabIndex;

    /// <summary>Anzahl der Pakete je Kategorie-Tab (unabhängig von Suche und Filter).</summary>
    [ObservableProperty]
    private int _tabCountAll;

    [ObservableProperty]
    private int _tabCountUser;

    [ObservableProperty]
    private int _tabCountSystemApp;

    [ObservableProperty]
    private int _tabCountSystemService;

    [ObservableProperty]
    private int _tabCountDisabled;

    [ObservableProperty]
    private int _tabCountRemoved;

    /// <summary>Der Plan, der gerade bestätigt werden soll (Dry-Run, S-08).</summary>
    [ObservableProperty]
    private PlannedAction? _pendingAction;

    /// <summary>True, wenn der Bestätigungsdialog vor einer Aktion angezeigt wird.</summary>
    [ObservableProperty]
    private bool _isActionConfirmVisible;

    /// <summary>
    /// Im Bestätigungsdialog eingetippter Paketname. Nur bei CAUTION nötig:
    /// die Aktion läuft erst, wenn der Name exakt passt (S-09/S-16).
    /// </summary>
    [ObservableProperty]
    private string _actionConfirmTypedName = string.Empty;

    /// <summary>True, wenn die Protokoll-Ansicht (S-14) eingeblendet ist.</summary>
    [ObservableProperty]
    private bool _isRollbackLogVisible;

    /// <summary>Inhalt der Protokoll-Ansicht; ohne Einträge der Hinweistext.</summary>
    [ObservableProperty]
    private string _rollbackLogText = string.Empty;

    /// <summary>True, wenn adb gefunden wurde.</summary>
    public bool IsAdbFound => AdbState.IsFound;

    /// <summary>True, wenn der Assistent angezeigt werden soll.</summary>
    public bool IsAdbMissing => !AdbState.IsFound && !IsBusy && !IsConfirmVisible;

    /// <summary>True, wenn ein Gerät ausgewählt und einsatzbereit ist.</summary>
    public bool HasOnlineDevice => SelectedDevice is { IsOnline: true };

    /// <summary>True, wenn Gerätedaten vorliegen.</summary>
    public bool HasDeviceInfo => DeviceInfo is not null;

    /// <summary>Anzeigetext der Version.</summary>
    public string AdbVersionText => string.IsNullOrEmpty(AdbState.Version)
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Strings.AdbVersionFormat, AdbState.Version);

    /// <summary>Anzeigetext des Pfads.</summary>
    public string AdbPathText => string.IsNullOrEmpty(AdbState.ExecutablePath)
        ? string.Empty
        : string.Format(CultureInfo.CurrentCulture, Strings.AdbPathFormat, AdbState.ExecutablePath);

    /// <summary>Anzeigetext der Quelle.</summary>
    public string AdbSourceText => AdbState.IsFound
        ? string.Format(CultureInfo.CurrentCulture, Strings.AdbSourceFormat, SourceLabel(AdbState.Source))
        : string.Empty;

    /// <summary>Anzeigetext des Modells.</summary>
    public string DeviceModelText => DeviceInfo?.Model is { Length: > 0 } model
        ? string.Format(CultureInfo.CurrentCulture, Strings.DeviceModelFormat, model)
        : string.Empty;

    /// <summary>Anzeigetext der Android-Version samt API-Level.</summary>
    public string DeviceAndroidText
    {
        get
        {
            if (DeviceInfo?.AndroidVersion is not { Length: > 0 } version)
            {
                return string.Empty;
            }

            return DeviceInfo.SdkLevel is { } sdk
                ? string.Format(CultureInfo.CurrentCulture, Strings.DeviceAndroidFormat, version, sdk)
                : string.Format(CultureInfo.CurrentCulture, Strings.DeviceAndroidNoSdkFormat, version);
        }
    }

    /// <summary>Anzeigetext der Seriennummer.</summary>
    public string DeviceSerialText => DeviceInfo is { } info
        ? string.Format(CultureInfo.CurrentCulture, Strings.DeviceSerialFormat, info.Serial)
        : string.Empty;

    /// <summary>Anzeigetext der Build-Kennung.</summary>
    public string DeviceBuildText => DeviceInfo?.BuildId is { Length: > 0 } build
        ? string.Format(CultureInfo.CurrentCulture, Strings.DeviceBuildFormat, build)
        : string.Empty;

    /// <summary>Anzahl der angezeigten bzw. aller Pakete.</summary>
    public string PackageCountText
    {
        get
        {
            var total = Packages.Count;
            if (total == 0)
            {
                return string.Empty;
            }

            var shown = FilteredPackages.Count;
            return shown == total
                ? string.Format(CultureInfo.CurrentCulture, Strings.PackageCountFormat, total)
                : string.Format(CultureInfo.CurrentCulture, Strings.PackageCountFilteredFormat, shown, total);
        }
    }

    /// <summary>Zusammenfassung des Downloads für die Bestätigung.</summary>
    public string ConfirmBody
    {
        get
        {
            if (PendingRelease is null || PendingArchive is null)
            {
                return string.Empty;
            }

            return string.Join(Environment.NewLine, new[]
            {
                $"{Strings.AdbConfirmVersion}: {PendingRelease.Version}",
                $"{Strings.AdbConfirmFile}: {PendingArchive.FileName}",
                $"{Strings.AdbConfirmSize}: {Humanize.Bytes(PendingArchive.SizeBytes)}",
                $"{Strings.AdbConfirmSha1}: {PendingArchive.Sha1}",
                string.Empty,
                Strings.AdbConfirmTarget,
                AppPaths.PlatformToolsFolder,
            });
        }
    }

    /// <summary>
    /// Zusammenfassung der geplanten Paket-Aktion für den Bestätigungsdialog:
    /// Paket, auszuführender Befehl und Rückweg als Dry-Run (S-08). Bei einer
    /// endgültigen Aktion (S-21) steht statt des Rückwegs der Hinweis, dass nur
    /// eine Neuinstallation über den Play Store möglich ist.
    /// </summary>
    public string ActionConfirmBody
    {
        get
        {
            if (PendingAction is not { } plan)
            {
                return string.Empty;
            }

            var package = plan.Package;
            var lines = new List<string>
            {
                $"{Strings.ActionConfirmPackage}: {package.DisplayText} ({package.PackageName})",
                string.Empty,
                $"{Strings.ActionConfirmCommand}: {plan.CommandText}",
            };

            if (!plan.IsPermanent)
            {
                lines.Add($"{Strings.ActionConfirmUndo}: {plan.RestoreCommandText}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    /// <summary>
    /// Zielgerät der geplanten Aktion: Hersteller/Modell, Seriennummer und
    /// Android-Version (S-18). Verhindert Änderungen am falschen Gerät.
    /// </summary>
    public string ActionConfirmDevice
    {
        get
        {
            if (PendingAction is null || SelectedDevice is not { } device)
            {
                return string.Empty;
            }

            var details = new List<string>();
            if (DeviceInfo is { } info)
            {
                var name = string.Join(
                    ' ',
                    new[] { info.Manufacturer, info.Model }.Where(part => !string.IsNullOrWhiteSpace(part)));
                if (name.Length > 0)
                {
                    details.Add($"{name} ({info.Serial})");
                }

                if (info.AndroidVersion is { Length: > 0 } version)
                {
                    details.Add(info.SdkLevel is { } sdk
                        ? string.Format(CultureInfo.CurrentCulture, Strings.DeviceAndroidFormat, version, sdk)
                        : string.Format(CultureInfo.CurrentCulture, Strings.DeviceAndroidNoSdkFormat, version));
                }
            }

            if (details.Count == 0)
            {
                details.Add(device.DisplayName);
            }

            return string.Format(
                CultureInfo.CurrentCulture,
                Strings.ActionConfirmDeviceFormat,
                string.Join(" · ", details));
        }
    }

    /// <summary>
    /// Warnhinweise zum Dialog: bei System-Apps kaum Speicherfreigabe (R-07),
    /// bei Nutzer-Apps kein Ein-Klick-Rollback (R-08) und die CAUTION-Hürde
    /// (S-16). Leer, wenn nichts zutrifft.
    /// </summary>
    public string ActionConfirmWarnings
    {
        get
        {
            if (PendingAction is not { } plan)
            {
                return string.Empty;
            }

            return string.Join(
                Environment.NewLine,
                ActionConfirmationRules.Warnings(plan.Action, plan.Package.Safety));
        }
    }

    /// <summary>True, wenn mindestens ein Warnhinweis angezeigt werden soll.</summary>
    public bool HasActionConfirmWarnings => ActionConfirmWarnings.Length > 0;

    /// <summary>
    /// True, wenn die Aktion den abgetippten Paketnamen verlangt (S-09/S-16:
    /// nur bei CAUTION/CRITICAL; SAFE-Pakete bestätigen per Klick).
    /// </summary>
    public bool ActionConfirmRequiresTyping
        => PendingAction is { } plan && ActionConfirmationRules.RequiresTyping(plan.Package.Safety);

    /// <summary>Der einzutippende Paketname (Platzhalter im Eingabefeld).</summary>
    public string ActionConfirmTypePlaceholder => PendingAction?.Package.PackageName ?? string.Empty;

    /// <summary>True, wenn in dieser Sitzung Änderungen gesichert wurden (S-04).</summary>
    public bool HasRollbackEntries => _executor?.Profile is { IsEmpty: false };

    /// <summary>Anzahl der gesicherten Änderungen als kurzer Text; leer, wenn keine.</summary>
    public string RollbackSummaryText
    {
        get
        {
            var profile = _executor?.Profile;
            return profile is { IsEmpty: false }
                ? string.Format(CultureInfo.CurrentCulture, Strings.RollbackSummaryFormat, profile.Entries.Count)
                : string.Empty;
        }
    }

    /// <summary>
    /// True, wenn der Ein-Klick-Rollback (S-06) ausgeführt werden darf: kein
    /// laufender Vorgang, gesicherte Änderungen und ein einsatzbereites Gerät.
    /// </summary>
    public bool CanRollback => !IsBusy && HasRollbackEntries && SelectedDevice is { IsOnline: true };

    private bool CanInteract() => !IsBusy;

    /// <summary>True, wenn exportiert werden darf: kein laufender Vorgang und sichtbare Pakete.</summary>
    private bool CanExport() => !IsBusy && FilteredPackages.Count > 0;

    /// <summary>
    /// True, wenn die Aktion ausgeführt werden darf: kein laufender Vorgang und
    /// bei CAUTION ein exakt eingetippter Paketname (S-09/S-16).
    /// </summary>
    private bool CanConfirmAction()
    {
        if (IsBusy)
        {
            return false;
        }

        if (!ActionConfirmRequiresTyping)
        {
            return true;
        }

        return string.Equals(
            ActionConfirmTypedName.Trim(),
            PendingAction?.Package.PackageName,
            StringComparison.Ordinal);
    }

    private void BeginBusy()
    {
        _busyCount++;
        IsBusy = true;
    }

    private void EndBusy()
    {
        _busyCount = Math.Max(0, _busyCount - 1);
        if (_busyCount == 0)
        {
            IsBusy = false;
            ProgressValue = 0;
        }
    }

    /// <summary>Erzeugt bei Bedarf einen Runner für die gefundene adb-Datei.</summary>
    private AdbRunner? CreateRunner()
        => AdbState.IsFound && !string.IsNullOrEmpty(AdbState.ExecutablePath)
            ? new AdbRunner(AdbState.ExecutablePath!)
            : null;

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task CheckAdbAsync()
    {
        BeginBusy();
        IsConfirmVisible = false;
        Status = Strings.StatusChecking;

        try
        {
            AdbState = await AdbDetector.DetectAsync(_settings.AdbPath).ConfigureAwait(true);
            Status = AdbState.IsFound
                ? (string.IsNullOrEmpty(AdbState.Version)
                    ? Strings.StatusAdbReady
                    : string.Format(CultureInfo.CurrentCulture, Strings.StatusAdbReadyFormat, AdbState.Version))
                : Strings.StatusAdbMissing;

            if (AdbState.IsFound)
            {
                await LoadDevicesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    /// <summary>Liest die angeschlossenen Geräte und wählt das erste einsatzbereite aus.</summary>
    private async Task LoadDevicesAsync()
    {
        var runner = CreateRunner();
        if (runner is null)
        {
            return;
        }

        var devices = await runner.GetDevicesAsync().ConfigureAwait(true);
        Devices = new ObservableCollection<AdbDevice>(devices);

        SelectedDevice = Devices.FirstOrDefault(device => device.IsOnline);

        if (SelectedDevice is null)
        {
            DeviceInfo = null;
            ClearPackages();
            Status = Devices.Any(device => device.IsUnauthorized)
                ? Strings.DeviceUnauthorized
                : Devices.Any(device => device.IsOffline)
                    ? Strings.DeviceOffline
                    : Strings.StatusNoDevice;
        }
    }

    /// <summary>Lädt Gerätedaten und Paketliste für das gewählte Gerät.</summary>
    private async Task LoadDeviceAndScanAsync(AdbDevice? device)
    {
        if (device is null)
        {
            DeviceInfo = null;
            ClearPackages();
            return;
        }

        if (!device.IsOnline)
        {
            DeviceInfo = null;
            ClearPackages();
            Status = device.IsUnauthorized ? Strings.DeviceUnauthorized : Strings.DeviceOffline;
            return;
        }

        var runner = CreateRunner();
        if (runner is null)
        {
            return;
        }

        runner.Serial = device.Serial;
        BeginBusy();

        try
        {
            Status = Strings.StatusChecking;
            var getProp = await runner.GetPropAsync().ConfigureAwait(true);
            DeviceInfo = DeviceInfoReader.Parse(getProp, device.Serial);

            await ScanPackagesAsync(runner).ConfigureAwait(true);

            Status = string.Format(
                CultureInfo.CurrentCulture,
                Strings.StatusDeviceSelectedFormat,
                device.DisplayName);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusScanErrorFormat, exception.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task ScanPackagesAsync()
    {
        var runner = CreateRunner();
        if (runner is null || SelectedDevice is not { IsOnline: true } device)
        {
            return;
        }

        runner.Serial = device.Serial;
        BeginBusy();

        try
        {
            await ScanPackagesAsync(runner).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusScanErrorFormat, exception.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    /// <summary>Führt den eigentlichen Scan aus und übernimmt das Ergebnis.</summary>
    private async Task ScanPackagesAsync(AdbRunner runner)
    {
        var progress = new Progress<PackageScanProgress>(OnScanProgress);
        var packages = await PackageScanner.ScanAsync(runner, progress).ConfigureAwait(true);

        Packages = new ObservableCollection<AndroidPackage>(packages);
        ApplyFilter();

        Status = string.Format(
            CultureInfo.CurrentCulture,
            Strings.ScanStageCompletedFormat,
            packages.Count);

        StartIconPreload(runner, Packages);
    }

    private void OnScanProgress(PackageScanProgress progress)
    {
        Status = progress.Stage switch
        {
            PackageScanStage.ListingPackages => Strings.ScanStageListing,
            PackageScanStage.ReadingLauncher => Strings.ScanStageLauncher,
            PackageScanStage.ReadingGames => Strings.ScanStageGames,
            PackageScanStage.ReadingDetails => Strings.ScanStageDetails,
            PackageScanStage.ReadingSizes => Strings.ScanStageSizes,
            _ => Status,
        };
    }

    /// <summary>
    /// Lädt Icon und Anzeigename eines Pakets bei Bedarf nach (auf Klick in der
    /// Icon-Spalte) und übernimmt beides in das Paket. Weitere Aufrufe sind durch
    /// den Ausführungs-Schutz des Befehls und den vorhandenen Cache günstig.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task LoadIconAsync(AndroidPackage? package)
    {
        if (package is null || package.HasIcon)
        {
            return;
        }

        var runner = CreateRunner();
        if (runner is null || SelectedDevice is not { IsOnline: true } device)
        {
            return;
        }

        runner.Serial = device.Serial;

        try
        {
            await ApplyIconAsync(runner, package, CancellationToken.None).ConfigureAwait(true);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusIconErrorFormat, exception.Message);
        }
    }

    /// <summary>
    /// Lädt nach einem Scan automatisch die Icons/Anzeigenamen der Apps und
    /// Spiele nach. Systemdienste (ohne Launcher-Eintrag) haben in der Regel
    /// kein Icon und werden dabei ausgelassen – sie lassen sich weiterhin per
    /// Klick nachladen. Das ist rein lesend (S-11 betrifft Aktionen am Gerät)
    /// und daher ohne Klick erlaubt; einmal Geladenes kommt danach aus dem Cache.
    /// </summary>
    private void StartIconPreload(AdbRunner runner, IReadOnlyList<AndroidPackage> packages)
    {
        CancelIconPreload();

        var cts = new CancellationTokenSource();
        _iconPreloadCts = cts;
        _ = PreloadIconsAsync(runner, packages, cts.Token);
    }

    /// <summary>Bricht ein laufendes automatisches Nachladen ab (z. B. bei neuer Auswahl).</summary>
    private void CancelIconPreload()
    {
        var cts = _iconPreloadCts;
        _iconPreloadCts = null;
        if (cts is null)
        {
            return;
        }

        cts.Cancel();
        cts.Dispose();
    }

    private async Task PreloadIconsAsync(
        AdbRunner runner,
        IReadOnlyList<AndroidPackage> packages,
        CancellationToken cancellationToken)
    {
        var targets = packages
            .Where(package => package.Category != PackageCategory.SystemService)
            .ToList();

        int total = targets.Count;
        int done = 0;

        try
        {
            foreach (var package in targets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!package.HasIcon)
                {
                    try
                    {
                        await ApplyIconAsync(runner, package, cancellationToken).ConfigureAwait(true);
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch
                    {
                        // Ein einzelnes Paket (z. B. ohne lesbares APK) darf den Rest nicht stoppen.
                    }
                }

                done++;
                Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusIconLoadingFormat, done, total);
            }
        }
        catch (OperationCanceledException)
        {
            // Abbruch durch neuen Scan bzw. Gerätewechsel: kein weiterer Text nötig.
        }
    }

    /// <summary>Füllt Icon und Anzeigename eines Pakets aus dem Cache oder frisch vom Gerät.</summary>
    private static async Task ApplyIconAsync(
        AdbRunner runner,
        AndroidPackage package,
        CancellationToken cancellationToken)
    {
        var result = await IconCache.LoadAsync(runner, package, cancellationToken).ConfigureAwait(true);

        if (result.Png is { Length: > 0 })
        {
            package.IconPng = result.Png;
        }

        if (!string.IsNullOrWhiteSpace(result.Label))
        {
            package.DisplayName = result.Label;
        }
    }

    /// <summary>Wendet Tab, Suchbegriff und Filter auf die Paketliste an.</summary>
    private void ApplyFilter()
    {
        IEnumerable<AndroidPackage> query = Packages;

        // Endgueltig geloeschte Pakete (S-21 "Delete data") verschwinden aus
        // allen Tabs, weil sie das Geraet nicht mehr fuehrt.
        query = query.Where(package => package.ExistsOnDevice);

        query = SelectedTabIndex switch
        {
            // Kategorie-Tabs: nur installierte UND aktivierte Pakete. Deaktivierte
            // wandern ausschliesslich in den Tab "Disabled", entfernte in "Removed".
            1 => query.Where(package =>
                package.Category == PackageCategory.UserInstalled && package.Installed && package.IsEnabled),
            2 => query.Where(package =>
                package.Category == PackageCategory.SystemApp && package.Installed && package.IsEnabled),
            3 => query.Where(package =>
                package.Category == PackageCategory.SystemService && package.Installed && package.IsEnabled),

            // Herkunftsuebergreifend: installiert und aktuell deaktiviert.
            // Entfernte Pakete (Installed = false) gehoeren nicht hierher.
            4 => query.Where(package => package.Installed && !package.IsEnabled),

            // Herkunftsuebergreifend: entfernt, aber mit behaltenen Daten
            // (z. B. "pm uninstall --user 0 -k"). System-Apps lassen sich per
            // "install-existing" zurueckholen, Nutzer-Apps nur ueber den Store.
            5 => query.Where(package => !package.Installed),
            _ => query,
        };

        var filterKind = SelectedFilterIndex >= 0 && SelectedFilterIndex < _filterKinds.Count
            ? _filterKinds[SelectedFilterIndex]
            : PackageFilterKind.All;

        query = filterKind switch
        {
            // Apps/Games trennen nach dem ApplicationInfo-Flag IS_GAME. Im Tab
            // "Removed" gibt es diese beiden Filter nicht (Apps und Spiele gemeinsam).
            PackageFilterKind.Apps => query.Where(package => !package.IsGame),
            PackageFilterKind.Games => query.Where(package => package.IsGame),
            PackageFilterKind.Bloatware => query.Where(package => package.IsBloatCandidate),
            PackageFilterKind.Privileged => query.Where(package => package.IsPrivilegedApp),
            _ => query,
        };

        var search = SearchText?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(package =>
                package.PackageName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || package.DisplayText.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        FilteredPackages = new ObservableCollection<AndroidPackage>(query);
        OnPropertyChanged(nameof(PackageCountText));
        UpdateTabCounts();
    }

    /// <summary>
    /// Passt die Filterauswahl an den gewechselten Tab an. Nur der Tab "Removed"
    /// weicht ab (ohne "Apps"/"Games"); alle übrigen Tabs teilen dieselbe Liste.
    /// Die gewählte Filterart bleibt erhalten, sofern sie im Ziel-Tab existiert,
    /// sonst fällt die Auswahl auf "All" zurück.
    /// </summary>
    private void UpdateFilterOptions(int tabIndex)
    {
        var kinds = FilterKindsForTab(tabIndex);
        if (kinds.Count == _filterKinds.Count)
        {
            return;
        }

        var current = SelectedFilterIndex >= 0 && SelectedFilterIndex < _filterKinds.Count
            ? _filterKinds[SelectedFilterIndex]
            : PackageFilterKind.All;

        _filterKinds = kinds;
        FilterOptions = FilterLabelsFor(kinds);

        var index = 0;
        for (var i = 0; i < kinds.Count; i++)
        {
            if (kinds[i] == current)
            {
                index = i;
                break;
            }
        }

        SelectedFilterIndex = index;
    }

    /// <summary>
    /// Zählt die Pakete je Tab nach denselben Regeln wie <see cref="ApplyFilter"/>
    /// (ohne Suche und Filter), damit die Zahl am Tab dem Tab-Inhalt entspricht.
    /// </summary>
    private void UpdateTabCounts()
    {
        var existing = Packages.Where(package => package.ExistsOnDevice).ToList();

        TabCountAll = existing.Count;
        TabCountUser = existing.Count(package =>
            package.Category == PackageCategory.UserInstalled && package.Installed && package.IsEnabled);
        TabCountSystemApp = existing.Count(package =>
            package.Category == PackageCategory.SystemApp && package.Installed && package.IsEnabled);
        TabCountSystemService = existing.Count(package =>
            package.Category == PackageCategory.SystemService && package.Installed && package.IsEnabled);
        TabCountDisabled = existing.Count(package => package.Installed && !package.IsEnabled);
        TabCountRemoved = existing.Count(package => !package.Installed);
    }

    private void ClearPackages()
    {
        CancelIconPreload();
        Packages = new ObservableCollection<AndroidPackage>();
        FilteredPackages = new ObservableCollection<AndroidPackage>();
        OnPropertyChanged(nameof(PackageCountText));
        UpdateTabCounts();
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task StartDownloadAsync()
    {
        BeginBusy();
        Status = Strings.AdbStageReadingManifest;

        try
        {
            var release = await _installer.GetReleaseAsync().ConfigureAwait(true);
            PendingRelease = release;
            PendingArchive = AdbInstaller.SelectArchive(release);
            IsConfirmVisible = true;
            Status = Strings.StatusReady;
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.AdbErrorFormat, exception.Message);
        }
        finally
        {
            EndBusy();
        }
    }

    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task ConfirmInstallAsync()
    {
        IsConfirmVisible = false;
        BeginBusy();

        var progress = new Progress<AdbInstallProgress>(OnInstallProgress);

        try
        {
            var state = await _installer.InstallAsync(progress).ConfigureAwait(true);
            AdbState = state;
            Status = string.IsNullOrEmpty(state.Version)
                ? Strings.StatusAdbReady
                : string.Format(CultureInfo.CurrentCulture, Strings.StatusAdbInstalledFormat, state.Version);

            if (state.IsFound)
            {
                await LoadDevicesAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.AdbErrorFormat, exception.Message);
        }
        finally
        {
            EndBusy();
            PendingRelease = null;
            PendingArchive = null;
        }
    }

    [RelayCommand]
    private void CancelConfirm()
    {
        IsConfirmVisible = false;
        PendingRelease = null;
        PendingArchive = null;
        Status = Strings.StatusReady;
    }

    /// <summary>
    /// Bereitet die vom Nutzer gewählte Aktion vor und zeigt den
    /// Bestätigungsdialog mit dem Dry-Run (S-08). Die möglichen Aktionen liefert
    /// <see cref="ActionPlanner.AvailableActions"/>; die Auswahl kommt fertig
    /// als <see cref="PlannedAction"/> aus dem Zeilen-Menü (M12).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInteract))]
    private void Act(PlannedAction? plan)
    {
        if (plan is null)
        {
            return;
        }

        PendingAction = plan;
        IsActionConfirmVisible = true;
    }

    /// <summary>
    /// Führt die bestätigte Aktion am Gerät aus (S-04/S-10/S-12) und meldet
    /// das Ergebnis im Status. Der Rückweg wird dabei vorab gesichert.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanConfirmAction))]
    private async Task ConfirmActionAsync()
    {
        var plan = PendingAction;
        IsActionConfirmVisible = false;

        if (plan is null)
        {
            return;
        }

        var runner = CreateRunner();
        if (runner is null || SelectedDevice is not { IsOnline: true } device)
        {
            Status = Strings.StatusNoDevice;
            PendingAction = null;
            return;
        }

        runner.Serial = device.Serial;
        BeginBusy();
        Status = string.Format(
            CultureInfo.CurrentCulture,
            Strings.StatusActionRunningFormat,
            plan.Package.PackageName);

        try
        {
            var executor = EnsureExecutor(runner, device);
            var outcome = await executor.ExecuteAsync(plan).ConfigureAwait(true);

            Status = outcome.Succeeded
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StatusActionDoneFormat,
                    plan.Package.DisplayText)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StatusActionFailedFormat,
                    outcome.Message ?? plan.Package.PackageName);

            ApplyFilter();
        }
        catch (Exception exception)
        {
            Status = string.Format(
                CultureInfo.CurrentCulture,
                Strings.StatusActionFailedFormat,
                exception.Message);
        }
        finally
        {
            PendingAction = null;
            EndBusy();
            NotifyRollbackState();
        }
    }

    /// <summary>Bricht den Bestätigungsdialog ohne Aktion ab.</summary>
    [RelayCommand]
    private void CancelAction()
    {
        IsActionConfirmVisible = false;
        PendingAction = null;
    }

    /// <summary>
    /// Ein-Klick-Rollback (S-06): nimmt alle in dieser Sitzung gesicherten
    /// Änderungen mit den exakten Rückwegen zurück, prüft den Zustand neu (S-10)
    /// und meldet das Ergebnis im Status.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRollback))]
    private async Task RollbackAllAsync()
    {
        var profile = _executor?.Profile;
        if (profile is null || profile.IsEmpty)
        {
            return;
        }

        var runner = CreateRunner();
        if (runner is null || SelectedDevice is not { IsOnline: true } device)
        {
            Status = Strings.StatusNoDevice;
            return;
        }

        runner.Serial = device.Serial;
        var total = profile.Entries.Count;
        BeginBusy();
        Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusRollbackRunningFormat, total);

        try
        {
            var executor = new RollbackExecutor(runner);
            var outcome = await executor.ExecuteAsync(profile, Packages).ConfigureAwait(true);

            Status = outcome.AllSucceeded
                ? string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StatusRollbackDoneFormat,
                    outcome.SucceededCount,
                    total)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    Strings.StatusRollbackPartialFormat,
                    outcome.SucceededCount,
                    total,
                    outcome.FailedCount);

            ApplyFilter();

            if (IsRollbackLogVisible)
            {
                await RefreshRollbackLogAsync().ConfigureAwait(true);
            }
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusActionFailedFormat, exception.Message);
        }
        finally
        {
            EndBusy();
            NotifyRollbackState();
        }
    }

    /// <summary>
    /// Schreibt auf Wunsch die Rettungs-Datei mit den Wiederherstellungsbefehlen
    /// zum manuellen Kopieren (S-05).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanRollback))]
    private async Task SaveRescueFileAsync()
    {
        var profile = _executor?.Profile;
        if (profile is null || profile.IsEmpty)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(AppPaths.RollbackFolder);
            var path = Path.Combine(
                AppPaths.RollbackFolder,
                RescueFileWriter.CreateFileName(DateTimeOffset.Now));

            await RescueFileWriter.WriteAsync(profile, path).ConfigureAwait(true);
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusRescueSavedFormat, path);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
        }
    }

    /// <summary>
    /// Schreibt die aktuell sichtbaren (gefilterten) Pakete als CSV, Markdown
    /// oder HTML in den Export-Ordner (M7 / F-09). Exportiert wird die Anzeige,
    /// nicht der gesamte Bestand.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    private async Task ExportAsync(ExportFormat format)
    {
        if (FilteredPackages.Count == 0)
        {
            Status = Strings.StatusExportEmpty;
            return;
        }

        try
        {
            var now = DateTimeOffset.Now;
            var path = Path.Combine(AppPaths.ExportFolder, ExportService.CreateFileName(format, now));
            var info = new ExportInfo(BuildExportDevice(), BuildExportAndroidVersion(), now);

            await ExportService.WriteAsync(format, FilteredPackages.ToList(), info, path).ConfigureAwait(true);
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusExportSavedFormat, path);
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
        }
    }

    /// <summary>Gerätebezeichnung für den Export-Kopf: Modell + Seriennummer, sonst Anzeigename.</summary>
    private string BuildExportDevice()
    {
        if (DeviceInfo is { } info)
        {
            var name = string.Join(
                ' ',
                new[] { info.Manufacturer, info.Model }.Where(part => !string.IsNullOrWhiteSpace(part)));
            if (name.Length > 0)
            {
                return $"{name} ({info.Serial})";
            }
        }

        return SelectedDevice?.DisplayName ?? Strings.ValueUnknown;
    }

    /// <summary>Android-Version für den Export-Kopf; null, wenn sie noch unbekannt ist.</summary>
    private string? BuildExportAndroidVersion()
        => DeviceAndroidText.Length > 0 ? DeviceAndroidText : null;

    /// <summary>Blendet die Protokoll-Ansicht ein oder aus (S-14).</summary>
    [RelayCommand]
    private async Task ToggleRollbackLogAsync()
    {
        IsRollbackLogVisible = !IsRollbackLogVisible;

        if (IsRollbackLogVisible)
        {
            await RefreshRollbackLogAsync().ConfigureAwait(true);
        }
    }

    /// <summary>Öffnet den Ordner mit Profilen, Rettungs-Datei und Protokoll.</summary>
    [RelayCommand]
    private void OpenRollbackFolder()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.AppFolder);
            Process.Start(new ProcessStartInfo(AppPaths.AppFolder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
        }
    }

    /// <summary>
    /// Öffnet die Einstellungen als Dialog (M8). Nach dem Schließen werden die
    /// Pakete neu bewertet (Whitelist/Experten-Mode ändern die Sperren) und –
    /// falls sich der ADB-Pfad geändert hat – die Erkennung erneut ausgeführt.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenSettingsAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not { } owner)
        {
            return;
        }

        var previousAdbPath = _settings.AdbPath;
        var window = new SettingsWindow { DataContext = new SettingsViewModel(_settings) };
        await window.ShowDialog(owner).ConfigureAwait(true);

        ReassessPackages();
        OnPropertyChanged(nameof(IsExpertModeActive));

        if (!string.Equals(previousAdbPath, _settings.AdbPath, StringComparison.Ordinal))
        {
            await CheckAdbAsync().ConfigureAwait(true);
        }
    }

    /// <summary>
    /// Öffnet das Hilfe-Fenster (M11) mit der Anleitung und dem About-Bereich.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanInteract))]
    private async Task OpenHelpAsync()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop
            || desktop.MainWindow is not { } owner)
        {
            return;
        }

        var window = new HelpWindow { DataContext = new HelpViewModel() };
        await window.ShowDialog(owner).ConfigureAwait(true);
    }

    /// <summary>
    /// Bewertet alle gescannten Pakete mit den aktuellen Schutzlisten neu und
    /// baut die Anzeige auf (nach einer Änderung der Einstellungen, M8).
    /// </summary>
    private void ReassessPackages()
    {
        foreach (var package in Packages)
        {
            BloatwareClassifier.Assess(package);
        }

        ApplyFilter();
    }

    /// <summary>Liest die Protokolldatei für die Ansicht ein (S-14).</summary>
    private async Task RefreshRollbackLogAsync()
    {
        try
        {
            RollbackLogText = File.Exists(AppPaths.ActionLogFile)
                ? (await File.ReadAllTextAsync(AppPaths.ActionLogFile).ConfigureAwait(true)).Trim()
                : string.Empty;
        }
        catch (Exception exception)
        {
            Status = string.Format(CultureInfo.CurrentCulture, Strings.StatusErrorFormat, exception.Message);
            RollbackLogText = string.Empty;
        }

        if (RollbackLogText.Length == 0)
        {
            RollbackLogText = Strings.RollbackLogEmptyNote;
        }
    }

    /// <summary>
    /// Meldet die vom Rollback-Profil abhängigen Anzeigewerte (Anzahl, Knöpfe).
    /// Aufgerufen, sobald sich die gesicherten Änderungen ändern können.
    /// </summary>
    private void NotifyRollbackState()
    {
        OnPropertyChanged(nameof(HasRollbackEntries));
        OnPropertyChanged(nameof(RollbackSummaryText));
        RollbackAllCommand.NotifyCanExecuteChanged();
        SaveRescueFileCommand.NotifyCanExecuteChanged();
    }

    /// <summary>
    /// Liefert den Ausführer der Sitzung für das angegebene Gerät. Er wird je
    /// Seriennummer einmal erzeugt, damit alle Änderungen für den späteren
    /// Ein-Klick-Rollback in einem Profil gesammelt werden (S-04/S-06).
    /// </summary>
    private ActionExecutor EnsureExecutor(AdbRunner runner, AdbDevice device)
    {
        if (_executor is null || !string.Equals(_executorSerial, device.Serial, StringComparison.Ordinal))
        {
            _executor = new ActionExecutor(runner);
            _executorSerial = device.Serial;
        }

        _executor.Profile.Device = device.DisplayName;
        return _executor;
    }

    private void OnInstallProgress(AdbInstallProgress progress)
    {
        switch (progress.Stage)
        {
            case AdbInstallStage.ReadingManifest:
                Status = Strings.AdbStageReadingManifest;
                ProgressValue = 0;
                break;

            case AdbInstallStage.Downloading:
                ProgressValue = progress.TotalBytes > 0
                    ? (double)progress.BytesReceived / progress.TotalBytes
                    : 0;
                Status = progress.TotalBytes > 0
                    ? string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AdbStageDownloadingFormat,
                        Humanize.Bytes(progress.BytesReceived),
                        Humanize.Bytes(progress.TotalBytes))
                    : string.Format(
                        CultureInfo.CurrentCulture,
                        Strings.AdbStageDownloadingUnknownFormat,
                        Humanize.Bytes(progress.BytesReceived));
                break;

            case AdbInstallStage.VerifyingChecksum:
                ProgressValue = 1;
                Status = Strings.AdbStageVerifying;
                break;

            case AdbInstallStage.Extracting:
                Status = Strings.AdbStageExtracting;
                break;

            case AdbInstallStage.VerifyingInstallation:
                Status = Strings.AdbStageVerifyingInstall;
                break;

            case AdbInstallStage.Completed:
                Status = Strings.StatusReady;
                break;
        }
    }

    private static string SourceLabel(AdbSource source) => source switch
    {
        AdbSource.Portable => Strings.AdbSourcePortable,
        AdbSource.EnvironmentVariable => Strings.AdbSourceEnvironment,
        AdbSource.Path => Strings.AdbSourcePath,
        AdbSource.CommonLocation => Strings.AdbSourceCommon,
        AdbSource.Winget => Strings.AdbSourceWinget,
        AdbSource.AppManaged => Strings.AdbSourceAppManaged,
        AdbSource.UserConfigured => Strings.AdbSourceUserConfigured,
        _ => string.Empty,
    };

    partial void OnAdbStateChanged(AdbStatus value) => RaiseStateDependentProperties();

    partial void OnIsBusyChanged(bool value)
    {
        RaiseStateDependentProperties();
        NotifyRollbackState();
    }

    partial void OnIsRollbackLogVisibleChanged(bool value) => OnPropertyChanged(nameof(RollbackLogButton));

    partial void OnIsConfirmVisibleChanged(bool value) => RaiseStateDependentProperties();

    partial void OnIsActionConfirmVisibleChanged(bool value) => RaiseStateDependentProperties();

    partial void OnPendingActionChanged(PlannedAction? value)
    {
        ActionConfirmTypedName = string.Empty;
        OnPropertyChanged(nameof(ActionConfirmBody));
        OnPropertyChanged(nameof(ActionConfirmNote));
        OnPropertyChanged(nameof(ActionConfirmDevice));
        OnPropertyChanged(nameof(ActionConfirmWarnings));
        OnPropertyChanged(nameof(HasActionConfirmWarnings));
        OnPropertyChanged(nameof(ActionConfirmRequiresTyping));
        OnPropertyChanged(nameof(ActionConfirmTypePlaceholder));
        ConfirmActionCommand.NotifyCanExecuteChanged();
    }

    partial void OnActionConfirmTypedNameChanged(string value)
        => ConfirmActionCommand.NotifyCanExecuteChanged();

    partial void OnPendingArchiveChanged(AdbReleaseArchive? value)
        => OnPropertyChanged(nameof(ConfirmBody));

    partial void OnDeviceInfoChanged(DeviceInfo? value)
    {
        OnPropertyChanged(nameof(HasDeviceInfo));
        OnPropertyChanged(nameof(DeviceModelText));
        OnPropertyChanged(nameof(DeviceAndroidText));
        OnPropertyChanged(nameof(DeviceSerialText));
        OnPropertyChanged(nameof(DeviceBuildText));
        OnPropertyChanged(nameof(ActionConfirmDevice));
    }

    partial void OnSelectedDeviceChanged(AdbDevice? value)
    {
        OnPropertyChanged(nameof(HasOnlineDevice));
        OnPropertyChanged(nameof(ActionConfirmDevice));
        NotifyRollbackState();
        _ = LoadDeviceAndScanAsync(value);
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedFilterIndexChanged(int value) => ApplyFilter();

    partial void OnSelectedTabIndexChanged(int value)
    {
        UpdateFilterOptions(value);
        ApplyFilter();
    }

    partial void OnFilteredPackagesChanged(ObservableCollection<AndroidPackage> value)
        => ExportCommand.NotifyCanExecuteChanged();

    private void RaiseStateDependentProperties()
    {
        OnPropertyChanged(nameof(IsAdbFound));
        OnPropertyChanged(nameof(IsAdbMissing));
        OnPropertyChanged(nameof(AdbVersionText));
        OnPropertyChanged(nameof(AdbPathText));
        OnPropertyChanged(nameof(AdbSourceText));
    }
}
