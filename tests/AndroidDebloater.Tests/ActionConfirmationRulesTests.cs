using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Models;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests der Regeln für den Aktions-Bestätigungsdialog (projektinfo.txt M6.4):
/// wann der Paketname abgetippt werden muss (S-09/S-16) und welche Hinweise
/// vorher erscheinen (R-07 Speicher, R-08 Rollback).
/// </summary>
public class ActionConfirmationRulesTests
{
    [Theory]
    [InlineData(PackageSafety.Safe, false)]
    [InlineData(PackageSafety.Caution, true)]
    [InlineData(PackageSafety.Critical, true)]
    public void RequiresTyping_OnlyForCautionAndCritical(PackageSafety safety, bool expected)
        => Assert.Equal(expected, ActionConfirmationRules.RequiresTyping(safety));

    [Fact]
    public void Warnings_SystemRemove_ShowsStorageHint()
    {
        var warnings = ActionConfirmationRules.Warnings(PackageAction.UninstallForUser, PackageSafety.Safe);

        Assert.Contains(Strings.ActionConfirmStorageWarning, warnings);
        Assert.DoesNotContain(Strings.ActionConfirmCautionWarning, warnings);
    }

    [Fact]
    public void Warnings_UserDisable_ShowsRollbackHint()
    {
        var warnings = ActionConfirmationRules.Warnings(PackageAction.DisableForUser, PackageSafety.Safe);

        Assert.Contains(Strings.ActionConfirmPlayWarning, warnings);
        Assert.DoesNotContain(Strings.ActionConfirmCautionWarning, warnings);
    }

    [Theory]
    [InlineData(PackageAction.UninstallPermanent)]
    [InlineData(PackageAction.UninstallKeepData)]
    public void Warnings_FinalUninstall_ShowsFinalHintWithoutCautionLine(PackageAction action)
    {
        // Eine endgültige Aktion warnt deutlich (S-21), auch wenn das Paket SAFE ist.
        var warnings = ActionConfirmationRules.Warnings(action, PackageSafety.Safe);

        Assert.Equal(new[] { Strings.ActionConfirmPermanentWarning }, warnings);
    }

    [Fact]
    public void Warnings_PurgeRemovedData_ShowsPurgeHint()
    {
        // "Delete data" warnt endgültig (S-21), auch wenn das Paket SAFE ist.
        var warnings = ActionConfirmationRules.Warnings(PackageAction.PurgeRemovedData, PackageSafety.Safe);

        Assert.Equal(new[] { Strings.ActionConfirmPurgeWarning }, warnings);
    }

    [Fact]
    public void Warnings_Caution_AddsTypingHintLast()
    {
        var warnings = ActionConfirmationRules.Warnings(PackageAction.UninstallForUser, PackageSafety.Caution);

        Assert.Equal(
            new[] { Strings.ActionConfirmStorageWarning, Strings.ActionConfirmCautionWarning },
            warnings);
    }

    [Fact]
    public void Warnings_Critical_ShowsExpertWarningInsteadOfCaution()
    {
        // CRITICAL ist nur im Experten-Mode erreichbar (S-20) und warnt deutlicher.
        var warnings = ActionConfirmationRules.Warnings(PackageAction.UninstallForUser, PackageSafety.Critical);

        Assert.Equal(
            new[] { Strings.ActionConfirmStorageWarning, Strings.ActionConfirmExpertWarning },
            warnings);
    }

    [Theory]
    [InlineData(PackageAction.InstallExisting)]
    [InlineData(PackageAction.EnableForUser)]
    public void Warnings_RestoreActions_HaveNoHints(PackageAction action)
    {
        var warnings = ActionConfirmationRules.Warnings(action, PackageSafety.Safe);

        Assert.Empty(warnings);
    }
}
