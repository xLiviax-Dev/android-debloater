using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Tests;

/// <summary>
/// Tests des Freischalt-Ablaufs für den Experten-Mode (projektinfo.txt S-20):
/// Einschalten ist erst nach ZWEI getrennten Bestätigungen möglich, Ausschalten
/// stellt die harte Sperre sofort wieder her.
/// </summary>
public class ExpertModeRulesTests
{
    [Theory]
    [InlineData(ExpertModeRules.StepOff, true, false)]
    [InlineData(ExpertModeRules.StepWarningAcknowledged, true, false)]
    [InlineData(ExpertModeRules.StepConsequencesAcknowledged, false, false)]
    [InlineData(ExpertModeRules.StepConsequencesAcknowledged, true, true)]
    public void CanEnable_RequiresSecondStepAndAck(int step, bool acknowledged, bool expected)
        => Assert.Equal(expected, ExpertModeRules.CanEnable(step, acknowledged));

    [Fact]
    public void Advance_WalksThroughBothConfirmationsAndStops()
    {
        var step = ExpertModeRules.Advance(ExpertModeRules.StepOff);
        Assert.Equal(ExpertModeRules.StepWarningAcknowledged, step);

        step = ExpertModeRules.Advance(step);
        Assert.Equal(ExpertModeRules.StepConsequencesAcknowledged, step);

        // Weitere Aufrufe ändern nichts mehr – der Ablauf hat genau zwei Stufen.
        Assert.Equal(ExpertModeRules.StepConsequencesAcknowledged, ExpertModeRules.Advance(step));
    }
}
