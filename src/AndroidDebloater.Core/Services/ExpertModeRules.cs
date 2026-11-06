namespace AndroidDebloater.Core.Services;

/// <summary>
/// Ablauf des Experten-Modes (projektinfo.txt S-20). Die Sicherheitslogik liegt
/// bewusst im Core (nicht in der Oberfläche): CRITICAL wird nur nach einer
/// deutlichen Warnung UND ZWEI getrennten Bestätigungen freigeschaltet.
/// </summary>
public static class ExpertModeRules
{
    /// <summary>Aus: die harte Sperre gilt (Ausgangszustand, S-07).</summary>
    public const int StepOff = 0;

    /// <summary>Erste Bestätigung: Warnung gelesen und fortgefahren.</summary>
    public const int StepWarningAcknowledged = 1;

    /// <summary>Zweite Bestätigung: Folgen ausdrücklich anerkannt.</summary>
    public const int StepConsequencesAcknowledged = 2;

    /// <summary>
    /// True, wenn das Einschalten erlaubt ist: erst nach der zweiten
    /// Bestätigung und mit gesetztem Anerkenntnis-Häkchen (S-20a).
    /// </summary>
    public static bool CanEnable(int step, bool acknowledgedConsequences)
        => step >= StepConsequencesAcknowledged && acknowledgedConsequences;

    /// <summary>Nächster Schritt, wenn die Warnung bestätigt wurde (maximal Schritt 2).</summary>
    public static int Advance(int step) => step switch
    {
        StepOff => StepWarningAcknowledged,
        StepWarningAcknowledged => StepConsequencesAcknowledged,
        _ => StepConsequencesAcknowledged,
    };
}
