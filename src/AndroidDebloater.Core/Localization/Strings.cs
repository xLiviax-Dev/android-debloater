using System.Resources;

namespace AndroidDebloater.Core.Localization;

/// <summary>
/// Zugriff auf die lokalisierten Texte. Jede Eigenschaft entspricht einem
/// Schlüssel in Strings.resx; fehlt ein Wert, wird der Schlüsselname
/// zurückgegeben (so fällt eine Lücke sofort auf).
/// </summary>
public static class Strings
{
    private static readonly ResourceManager Resources =
        new("AndroidDebloater.Core.Localization.Strings", typeof(Strings).Assembly);

    public static string AppTitle => Get(nameof(AppTitle));
    public static string ShellHint => Get(nameof(ShellHint));
    public static string StatusReady => Get(nameof(StatusReady));
    public static string StatusChecking => Get(nameof(StatusChecking));
    public static string StatusAdbReady => Get(nameof(StatusAdbReady));
    public static string StatusAdbReadyFormat => Get(nameof(StatusAdbReadyFormat));
    public static string StatusAdbMissing => Get(nameof(StatusAdbMissing));
    public static string StatusAdbInstalledFormat => Get(nameof(StatusAdbInstalledFormat));
    public static string StatusErrorFormat => Get(nameof(StatusErrorFormat));

    public static string AdbCardTitle => Get(nameof(AdbCardTitle));
    public static string AdbVersionFormat => Get(nameof(AdbVersionFormat));
    public static string AdbPathFormat => Get(nameof(AdbPathFormat));
    public static string AdbSourceFormat => Get(nameof(AdbSourceFormat));
    public static string AdbCheckButton => Get(nameof(AdbCheckButton));
    public static string AdbDownloadButton => Get(nameof(AdbDownloadButton));
    public static string AdbAssistantBody => Get(nameof(AdbAssistantBody));

    public static string AdbConfirmTitle => Get(nameof(AdbConfirmTitle));
    public static string AdbConfirmVersion => Get(nameof(AdbConfirmVersion));
    public static string AdbConfirmFile => Get(nameof(AdbConfirmFile));
    public static string AdbConfirmSize => Get(nameof(AdbConfirmSize));
    public static string AdbConfirmSha1 => Get(nameof(AdbConfirmSha1));
    public static string AdbConfirmTarget => Get(nameof(AdbConfirmTarget));
    public static string AdbConfirmNote => Get(nameof(AdbConfirmNote));
    public static string AdbConfirmYes => Get(nameof(AdbConfirmYes));
    public static string AdbConfirmCancel => Get(nameof(AdbConfirmCancel));

    public static string AdbStageReadingManifest => Get(nameof(AdbStageReadingManifest));
    public static string AdbStageDownloadingFormat => Get(nameof(AdbStageDownloadingFormat));
    public static string AdbStageDownloadingUnknownFormat => Get(nameof(AdbStageDownloadingUnknownFormat));
    public static string AdbStageVerifying => Get(nameof(AdbStageVerifying));
    public static string AdbStageExtracting => Get(nameof(AdbStageExtracting));
    public static string AdbStageVerifyingInstall => Get(nameof(AdbStageVerifyingInstall));
    public static string AdbInstalledFormat => Get(nameof(AdbInstalledFormat));
    public static string AdbErrorFormat => Get(nameof(AdbErrorFormat));

    public static string AdbSourcePortable => Get(nameof(AdbSourcePortable));
    public static string AdbSourceEnvironment => Get(nameof(AdbSourceEnvironment));
    public static string AdbSourcePath => Get(nameof(AdbSourcePath));
    public static string AdbSourceCommon => Get(nameof(AdbSourceCommon));
    public static string AdbSourceWinget => Get(nameof(AdbSourceWinget));
    public static string AdbSourceAppManaged => Get(nameof(AdbSourceAppManaged));

    public static string ValueUnknown => Get(nameof(ValueUnknown));
    public static string KindSystem => Get(nameof(KindSystem));
    public static string KindUser => Get(nameof(KindUser));
    public static string StatusActive => Get(nameof(StatusActive));
    public static string StatusDisabled => Get(nameof(StatusDisabled));
    public static string StatusHidden => Get(nameof(StatusHidden));
    public static string StatusSuspended => Get(nameof(StatusSuspended));
    public static string StatusRemoved => Get(nameof(StatusRemoved));

    public static string DeviceCardTitle => Get(nameof(DeviceCardTitle));
    public static string DeviceNoneConnected => Get(nameof(DeviceNoneConnected));
    public static string DeviceUnauthorized => Get(nameof(DeviceUnauthorized));
    public static string DeviceOffline => Get(nameof(DeviceOffline));
    public static string DeviceRefreshButton => Get(nameof(DeviceRefreshButton));
    public static string DeviceSerialFormat => Get(nameof(DeviceSerialFormat));
    public static string DeviceModelFormat => Get(nameof(DeviceModelFormat));
    public static string DeviceAndroidFormat => Get(nameof(DeviceAndroidFormat));
    public static string DeviceAndroidNoSdkFormat => Get(nameof(DeviceAndroidNoSdkFormat));
    public static string DeviceBuildFormat => Get(nameof(DeviceBuildFormat));

    public static string PackageCardTitle => Get(nameof(PackageCardTitle));
    public static string PackageSearchPlaceholder => Get(nameof(PackageSearchPlaceholder));
    public static string PackageTabAll => Get(nameof(PackageTabAll));
    public static string PackageTabUser => Get(nameof(PackageTabUser));
    public static string PackageTabSystemApp => Get(nameof(PackageTabSystemApp));
    public static string PackageTabSystemService => Get(nameof(PackageTabSystemService));
    public static string PackageTabDisabled => Get(nameof(PackageTabDisabled));
    public static string PackageTabRemoved => Get(nameof(PackageTabRemoved));
    public static string PackageFilterAll => Get(nameof(PackageFilterAll));
    public static string PackageFilterApps => Get(nameof(PackageFilterApps));
    public static string PackageFilterGames => Get(nameof(PackageFilterGames));
    public static string PackageFilterPrivileged => Get(nameof(PackageFilterPrivileged));
    public static string PackageReloadButton => Get(nameof(PackageReloadButton));
    public static string PackageCountFormat => Get(nameof(PackageCountFormat));
    public static string PackageCountFilteredFormat => Get(nameof(PackageCountFilteredFormat));
    public static string ColumnPackage => Get(nameof(ColumnPackage));
    public static string ColumnIcon => Get(nameof(ColumnIcon));
    public static string PackageLoadIconHint => Get(nameof(PackageLoadIconHint));
    public static string ColumnKind => Get(nameof(ColumnKind));
    public static string ColumnStatus => Get(nameof(ColumnStatus));
    public static string ColumnSize => Get(nameof(ColumnSize));
    public static string ColumnInstalled => Get(nameof(ColumnInstalled));
    public static string ColumnInstaller => Get(nameof(ColumnInstaller));
    public static string ColumnVersion => Get(nameof(ColumnVersion));

    public static string ScanStageListing => Get(nameof(ScanStageListing));
    public static string ScanStageLauncher => Get(nameof(ScanStageLauncher));
    public static string ScanStageGames => Get(nameof(ScanStageGames));
    public static string ScanStageDetails => Get(nameof(ScanStageDetails));
    public static string ScanStageSizes => Get(nameof(ScanStageSizes));
    public static string ScanStageCompletedFormat => Get(nameof(ScanStageCompletedFormat));
    public static string StatusNoDevice => Get(nameof(StatusNoDevice));
    public static string StatusDeviceSelectedFormat => Get(nameof(StatusDeviceSelectedFormat));
    public static string StatusScanErrorFormat => Get(nameof(StatusScanErrorFormat));
    public static string StatusIconErrorFormat => Get(nameof(StatusIconErrorFormat));
    public static string StatusIconLoadingFormat => Get(nameof(StatusIconLoadingFormat));

    public static string SafetySafe => Get(nameof(SafetySafe));
    public static string SafetyCaution => Get(nameof(SafetyCaution));
    public static string SafetyCritical => Get(nameof(SafetyCritical));
    public static string ColumnSafety => Get(nameof(ColumnSafety));
    public static string ColumnBloat => Get(nameof(ColumnBloat));
    public static string PackageFilterBloatware => Get(nameof(PackageFilterBloatware));

    public static string ReasonWhitelist => Get(nameof(ReasonWhitelist));
    public static string ReasonCritical => Get(nameof(ReasonCritical));
    public static string ReasonUserInstalled => Get(nameof(ReasonUserInstalled));
    public static string ReasonUserInstalledViaFormat => Get(nameof(ReasonUserInstalledViaFormat));
    public static string ReasonSystemImage => Get(nameof(ReasonSystemImage));
    public static string ReasonOemPreload => Get(nameof(ReasonOemPreload));
    public static string ReasonPlayAutoInstall => Get(nameof(ReasonPlayAutoInstall));
    public static string ReasonStrongPatternFormat => Get(nameof(ReasonStrongPatternFormat));
    public static string ReasonWeakPatternFormat => Get(nameof(ReasonWeakPatternFormat));
    public static string ReasonKnownPromo => Get(nameof(ReasonKnownPromo));
    public static string ReasonSystemInternal => Get(nameof(ReasonSystemInternal));

    public static string ActionAbortedNoteFormat => Get(nameof(ActionAbortedNoteFormat));
    public static string ActionFallbackNote => Get(nameof(ActionFallbackNote));
    public static string ActionFailedNoteFormat => Get(nameof(ActionFailedNoteFormat));
    public static string ActionUnconfirmedNote => Get(nameof(ActionUnconfirmedNote));
    public static string ActionUnknownNote => Get(nameof(ActionUnknownNote));

    public static string ColumnAction => Get(nameof(ColumnAction));
    public static string ActionMenuButton => Get(nameof(ActionMenuButton));
    public static string ActionRemove => Get(nameof(ActionRemove));
    public static string ActionDisable => Get(nameof(ActionDisable));
    public static string ActionUninstallPermanent => Get(nameof(ActionUninstallPermanent));
    public static string ActionUninstallKeepData => Get(nameof(ActionUninstallKeepData));
    public static string ActionPurgeData => Get(nameof(ActionPurgeData));
    public static string ActionRestore => Get(nameof(ActionRestore));
    public static string ActionEnable => Get(nameof(ActionEnable));
    public static string ActionConfirmTitle => Get(nameof(ActionConfirmTitle));
    public static string ActionConfirmPackage => Get(nameof(ActionConfirmPackage));
    public static string ActionConfirmAction => Get(nameof(ActionConfirmAction));
    public static string ActionConfirmCommand => Get(nameof(ActionConfirmCommand));
    public static string ActionConfirmUndo => Get(nameof(ActionConfirmUndo));
    public static string ActionConfirmNote => Get(nameof(ActionConfirmNote));
    public static string ActionConfirmYes => Get(nameof(ActionConfirmYes));
    public static string ActionConfirmCancel => Get(nameof(ActionConfirmCancel));
    public static string StatusActionNotPossible => Get(nameof(StatusActionNotPossible));
    public static string StatusActionRunningFormat => Get(nameof(StatusActionRunningFormat));
    public static string StatusActionDoneFormat => Get(nameof(StatusActionDoneFormat));
    public static string StatusActionFailedFormat => Get(nameof(StatusActionFailedFormat));
    public static string ActionConfirmDeviceFormat => Get(nameof(ActionConfirmDeviceFormat));
    public static string ActionConfirmStorageWarning => Get(nameof(ActionConfirmStorageWarning));
    public static string ActionConfirmPlayWarning => Get(nameof(ActionConfirmPlayWarning));
    public static string ActionConfirmCautionWarning => Get(nameof(ActionConfirmCautionWarning));
    public static string ActionConfirmPermanentWarning => Get(nameof(ActionConfirmPermanentWarning));
    public static string ActionConfirmPermanentNote => Get(nameof(ActionConfirmPermanentNote));
    public static string ActionConfirmPurgeWarning => Get(nameof(ActionConfirmPurgeWarning));
    public static string ActionConfirmTypeLabel => Get(nameof(ActionConfirmTypeLabel));

    public static string RollbackButton => Get(nameof(RollbackButton));
    public static string RollbackSummaryFormat => Get(nameof(RollbackSummaryFormat));
    public static string RollbackNote => Get(nameof(RollbackNote));
    public static string RollbackLogButton => Get(nameof(RollbackLogButton));
    public static string RollbackHideLogButton => Get(nameof(RollbackHideLogButton));
    public static string RollbackOpenFolderButton => Get(nameof(RollbackOpenFolderButton));
    public static string RollbackLogTitle => Get(nameof(RollbackLogTitle));
    public static string RollbackLogEmptyNote => Get(nameof(RollbackLogEmptyNote));
    public static string RollbackRescueButton => Get(nameof(RollbackRescueButton));
    public static string StatusRollbackRunningFormat => Get(nameof(StatusRollbackRunningFormat));
    public static string StatusRollbackDoneFormat => Get(nameof(StatusRollbackDoneFormat));
    public static string StatusRollbackPartialFormat => Get(nameof(StatusRollbackPartialFormat));
    public static string StatusRescueSavedFormat => Get(nameof(StatusRescueSavedFormat));
    public static string RollbackNotRestorableNote => Get(nameof(RollbackNotRestorableNote));

    public static string ColumnName => Get(nameof(ColumnName));
    public static string ExportTitle => Get(nameof(ExportTitle));
    public static string ExportButton => Get(nameof(ExportButton));
    public static string ExportCsvOption => Get(nameof(ExportCsvOption));
    public static string ExportMarkdownOption => Get(nameof(ExportMarkdownOption));
    public static string ExportHtmlOption => Get(nameof(ExportHtmlOption));
    public static string ExportDeviceFormat => Get(nameof(ExportDeviceFormat));
    public static string ExportCreatedFormat => Get(nameof(ExportCreatedFormat));
    public static string ExportCountFormat => Get(nameof(ExportCountFormat));
    public static string StatusExportSavedFormat => Get(nameof(StatusExportSavedFormat));
    public static string StatusExportEmpty => Get(nameof(StatusExportEmpty));

    public static string SettingsButton => Get(nameof(SettingsButton));
    public static string SettingsTitle => Get(nameof(SettingsTitle));
    public static string SettingsSaveButton => Get(nameof(SettingsSaveButton));
    public static string SettingsCloseButton => Get(nameof(SettingsCloseButton));
    public static string SettingsCancelButton => Get(nameof(SettingsCancelButton));
    public static string SettingsSavedStatus => Get(nameof(SettingsSavedStatus));
    public static string SettingsLanguageLabel => Get(nameof(SettingsLanguageLabel));
    public static string SettingsLanguageHint => Get(nameof(SettingsLanguageHint));
    public static string SettingsWhitelistLabel => Get(nameof(SettingsWhitelistLabel));
    public static string SettingsWhitelistHint => Get(nameof(SettingsWhitelistHint));
    public static string SettingsWhitelistPlaceholder => Get(nameof(SettingsWhitelistPlaceholder));
    public static string SettingsWhitelistAddButton => Get(nameof(SettingsWhitelistAddButton));
    public static string SettingsWhitelistRemoveButton => Get(nameof(SettingsWhitelistRemoveButton));
    public static string SettingsWhitelistResetButton => Get(nameof(SettingsWhitelistResetButton));
    public static string SettingsWhitelistEmptyNote => Get(nameof(SettingsWhitelistEmptyNote));
    public static string SettingsAdbPathLabel => Get(nameof(SettingsAdbPathLabel));
    public static string SettingsAdbPathHint => Get(nameof(SettingsAdbPathHint));
    public static string SettingsAdbPathAutomaticButton => Get(nameof(SettingsAdbPathAutomaticButton));
    public static string SettingsIconCacheLabel => Get(nameof(SettingsIconCacheLabel));
    public static string SettingsIconCacheUsageFormat => Get(nameof(SettingsIconCacheUsageFormat));
    public static string SettingsIconCacheEmpty => Get(nameof(SettingsIconCacheEmpty));
    public static string SettingsClearIconCacheButton => Get(nameof(SettingsClearIconCacheButton));
    public static string StatusIconCacheClearedFormat => Get(nameof(StatusIconCacheClearedFormat));
    public static string SettingsGameListLabel => Get(nameof(SettingsGameListLabel));
    public static string SettingsGameListUsageFormat => Get(nameof(SettingsGameListUsageFormat));
    public static string SettingsGameListEmpty => Get(nameof(SettingsGameListEmpty));
    public static string SettingsClearGameListButton => Get(nameof(SettingsClearGameListButton));
    public static string StatusGameListClearedFormat => Get(nameof(StatusGameListClearedFormat));
    public static string SettingsExpertLabel => Get(nameof(SettingsExpertLabel));
    public static string SettingsExpertOffText => Get(nameof(SettingsExpertOffText));
    public static string SettingsExpertOnText => Get(nameof(SettingsExpertOnText));
    public static string SettingsExpertOpenButton => Get(nameof(SettingsExpertOpenButton));
    public static string SettingsExpertStep1Text => Get(nameof(SettingsExpertStep1Text));
    public static string SettingsExpertStep2Text => Get(nameof(SettingsExpertStep2Text));
    public static string SettingsExpertAck => Get(nameof(SettingsExpertAck));
    public static string SettingsExpertContinueButton => Get(nameof(SettingsExpertContinueButton));
    public static string SettingsExpertEnableButton => Get(nameof(SettingsExpertEnableButton));
    public static string SettingsExpertDisableButton => Get(nameof(SettingsExpertDisableButton));
    public static string ExpertModeBannerText => Get(nameof(ExpertModeBannerText));
    public static string StatusExpertModeEnabled => Get(nameof(StatusExpertModeEnabled));
    public static string StatusExpertModeDisabled => Get(nameof(StatusExpertModeDisabled));
    public static string ActionConfirmExpertWarning => Get(nameof(ActionConfirmExpertWarning));
    public static string AdbSourceUserConfigured => Get(nameof(AdbSourceUserConfigured));

    public static string HelpButton => Get(nameof(HelpButton));
    public static string ThemeToggleTip => Get(nameof(ThemeToggleTip));
    public static string AppSubtitle => Get(nameof(AppSubtitle));
    public static string ChipAdbReady => Get(nameof(ChipAdbReady));
    public static string ChipAdbMissing => Get(nameof(ChipAdbMissing));
    public static string ChipNoDevice => Get(nameof(ChipNoDevice));
    public static string HelpTitle => Get(nameof(HelpTitle));
    public static string HelpTabUserGuide => Get(nameof(HelpTabUserGuide));
    public static string HelpTabAbout => Get(nameof(HelpTabAbout));
    public static string HelpCloseButton => Get(nameof(HelpCloseButton));
    public static string AboutVersionFormat => Get(nameof(AboutVersionFormat));
    public static string AboutLicenseText => Get(nameof(AboutLicenseText));
    public static string AboutComponentsLabel => Get(nameof(AboutComponentsLabel));
    public static string AboutComponentsBody => Get(nameof(AboutComponentsBody));
    public static string AboutDisclaimerText => Get(nameof(AboutDisclaimerText));
    public static string AboutWarningText => Get(nameof(AboutWarningText));

    private static string Get(string key)
        => Resources.GetString(key) ?? key;
}
