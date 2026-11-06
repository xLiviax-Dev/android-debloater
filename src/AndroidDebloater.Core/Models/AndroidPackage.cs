using System.ComponentModel;
using System.Globalization;
using AndroidDebloater.Core.Localization;
using AndroidDebloater.Core.Services;

namespace AndroidDebloater.Core.Models;

/// <summary>
/// Ein einzelnes Android-Paket mit Zustand, Größen und Herkunft.
/// Die Werte stammen ausschließlich aus echten Geräteabfragen
/// (pm list / dumpsys) – es wird nichts geraten.
/// </summary>
public sealed class AndroidPackage : INotifyPropertyChanged
{
    private string? _displayName;
    private byte[]? _iconPng;
    private PackageKind _kind;
    private bool _installed = true;
    private bool _existsOnDevice = true;
    private bool _hidden;
    private bool _suspended;
    private bool _stopped;
    private int _enabledState;

    /// <summary>Vollständiger Paketname, z. B. "com.android.fmradio".</summary>
    public required string PackageName { get; init; }

    /// <summary>Pfad des/der APK(s) (codePath aus dumpsys).</summary>
    public string? CodePath { get; set; }

    /// <summary>Herkunft: System oder Benutzer.</summary>
    public PackageKind Kind
    {
        get => _kind;
        set
        {
            if (_kind != value)
            {
                _kind = value;
                OnPropertyChanged(nameof(Kind));
                OnPropertyChanged(nameof(KindText));
                OnPropertyChanged(nameof(Category));
                OnPropertyChanged(nameof(IsUpdatedSystemApp));
                NotifyActionState();
            }
        }
    }

    /// <summary>
    /// True, wenn das Paket eine Launcher-Aktivität anbietet und damit im
    /// App-Drawer sichtbar ist. Grundlage der Unterscheidung von System-Apps
    /// und Systemdiensten (siehe <see cref="Category"/>).
    /// </summary>
    public bool HasLauncherEntry { get; set; }

    /// <summary>
    /// Anzeigegruppe für die Tab-Aufteilung: nachinstalliert, System-App/Spiel
    /// (mit Launcher) oder Systemdienst (ohne Launcher).
    /// </summary>
    public PackageCategory Category => Kind == PackageKind.User
        ? PackageCategory.UserInstalled
        : HasLauncherEntry
            ? PackageCategory.SystemApp
            : PackageCategory.SystemService;

    /// <summary>True bei privilegierten System-Apps (/priv-app oder Flag PRIVILEGED).</summary>
    public bool IsPrivilegedApp { get; set; }

    /// <summary>
    /// True, wenn das Gerät das Paket als Spiel führt (ApplicationInfo-Flag
    /// IS_GAME, ermittelt aus der Liste des GameManagerService per "dumpsys game").
    /// </summary>
    public bool IsGame { get; set; }

    /// <summary>True, wenn das Paket für Benutzer 0 installiert ist.</summary>
    public bool Installed
    {
        get => _installed;
        set
        {
            if (_installed != value)
            {
                _installed = value;
                OnPropertyChanged(nameof(Installed));
                NotifyActionState();
            }
        }
    }

    /// <summary>
    /// True, wenn das Paket vom Gerät überhaupt noch geführt wird – auch im
    /// Zustand "entfernt, Daten behalten". Nach dem endgültigen Löschen der
    /// Restdaten (S-21) liefert "dumpsys" kein Paket mehr, der Wert wird dann
    /// false und die Zeile verschwindet aus allen Tabs.
    /// </summary>
    public bool ExistsOnDevice
    {
        get => _existsOnDevice;
        set
        {
            if (_existsOnDevice != value)
            {
                _existsOnDevice = value;
                OnPropertyChanged(nameof(ExistsOnDevice));
                NotifyActionState();
            }
        }
    }

    /// <summary>True, wenn das Paket ausgeblendet ist (hidden).</summary>
    public bool Hidden
    {
        get => _hidden;
        set
        {
            if (_hidden != value)
            {
                _hidden = value;
                OnPropertyChanged(nameof(Hidden));
                NotifyActionState();
            }
        }
    }

    /// <summary>True, wenn das Paket suspendiert ist.</summary>
    public bool Suspended
    {
        get => _suspended;
        set
        {
            if (_suspended != value)
            {
                _suspended = value;
                OnPropertyChanged(nameof(Suspended));
                NotifyActionState();
            }
        }
    }

    /// <summary>True, wenn das Paket gestoppt ist (stopped).</summary>
    public bool Stopped
    {
        get => _stopped;
        set
        {
            if (_stopped != value)
            {
                _stopped = value;
                OnPropertyChanged(nameof(Stopped));
                NotifyActionState();
            }
        }
    }

    /// <summary>
    /// Enabled-Zustand laut dumpsys (COMPONENT_ENABLED_STATE_*): 0 = DEFAULT
    /// und 1 = ENABLED bedeuten aktiv, 2 = DISABLED, 3 = DISABLED_USER und
    /// 4 = DISABLED_UNTIL_USED bedeuten deaktiviert.
    /// </summary>
    public int EnabledState
    {
        get => _enabledState;
        set
        {
            if (_enabledState != value)
            {
                _enabledState = value;
                OnPropertyChanged(nameof(EnabledState));
                NotifyActionState();
            }
        }
    }

    /// <summary>Paketname des Installierers (z. B. com.android.vending), sonst null.</summary>
    public string? InstallerPackage { get; set; }

    /// <summary>Zeitpunkt der Erstinstallation.</summary>
    public DateTimeOffset? FirstInstallTime { get; set; }

    /// <summary>Versionsname aus dem Paketmanifest.</summary>
    public string? VersionName { get; set; }

    /// <summary>
    /// Anzeigename aus dem APK-Label des Geräts; null, solange Icon und Name
    /// noch nicht geladen wurden (siehe <c>IconCache</c>).
    /// </summary>
    public string? DisplayName
    {
        get => _displayName;
        set
        {
            if (!string.Equals(_displayName, value, StringComparison.Ordinal))
            {
                _displayName = value;
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(DisplayText));
                OnPropertyChanged(nameof(AvatarText));
                OnPropertyChanged(nameof(HasLabel));
            }
        }
    }

    /// <summary>PNG-Daten des Launcher-Icons; null, solange es nicht geladen wurde.</summary>
    public byte[]? IconPng
    {
        get => _iconPng;
        set
        {
            if (!ReferenceEquals(_iconPng, value))
            {
                _iconPng = value;
                OnPropertyChanged(nameof(IconPng));
                OnPropertyChanged(nameof(HasIcon));
            }
        }
    }

    /// <summary>True, wenn bereits ein Icon-Bild vorliegt.</summary>
    public bool HasIcon => _iconPng is { Length: > 0 };

    /// <summary>True, wenn ein Gerätelabel vorliegt (dann zeigt die Zeile den Paketnamen zusätzlich darunter).</summary>
    public bool HasLabel => !string.IsNullOrWhiteSpace(_displayName);

    /// <summary>Name für die Anzeige: Gerätelabel, sonst der Paketname.</summary>
    public string DisplayText => string.IsNullOrWhiteSpace(_displayName) ? PackageName : _displayName!;

    /// <summary>Ersatzdarstellung ohne Icon: Anfangsbuchstabe des Anzeigenamens.</summary>
    public string AvatarText => DisplayText.Length > 0
        ? char.ToUpperInvariant(DisplayText[0]).ToString()
        : "?";

    /// <summary>
    /// Gefahren-Einstufung (Abschnitt 6.2). Standard ist <see cref="PackageSafety.Caution"/>,
    /// damit ein nicht bewertetes Paket nie versehentlich als unbedenklich gilt.
    /// </summary>
    public PackageSafety Safety { get; set; } = PackageSafety.Caution;

    /// <summary>Bloatware-Bewertung 0..100 (0 = unauffällig); Schwelle siehe Klassifizierer.</summary>
    public int BloatScore { get; set; }

    /// <summary>Nachvollziehbare Begründungen zur Bewertung (für die Anzeige).</summary>
    public IReadOnlyList<string> BloatReasons { get; set; } = Array.Empty<string>();

    /// <summary>True, wenn das Paket auf der Schutzliste steht (Standard: nie entfernbar).</summary>
    public bool IsProtected { get; set; }

    /// <summary>Größe der APK(s) in Bytes.</summary>
    public long ApkSizeBytes { get; set; }

    /// <summary>Größe der App-Daten in Bytes.</summary>
    public long DataSizeBytes { get; set; }

    /// <summary>Größe des Caches in Bytes.</summary>
    public long CacheSizeBytes { get; set; }

    /// <summary>Gesamtgröße (APK + Daten + Cache) in Bytes.</summary>
    public long TotalSizeBytes => ApkSizeBytes + DataSizeBytes + CacheSizeBytes;

    /// <summary>
    /// True, wenn das Paket aktiv ist (nicht deaktiviert). Am TB311FU geprüft:
    /// Zustand 0 (DEFAULT) und 1 (ENABLED) stehen in "pm list packages -e",
    /// Zustand 2/3/4 in "pm list packages -d".
    /// </summary>
    public bool IsEnabled => EnabledState is 0 or 1;

    /// <summary>
    /// True, wenn für dieses Paket mindestens eine Aktion möglich ist
    /// (geschützte und kritische Pakete bleiben gesperrt – siehe ActionPlanner).
    /// Grundlage für die Anzeige des Aktions-Menüs in der Zeile (M12).
    /// </summary>
    public bool HasActions => ActionPlanner.AvailableActions(this).Count > 0;

    /// <summary>
    /// True, wenn es sich um eine aktualisierte System-App handelt: Der Code liegt
    /// in /data/app, das Paket ist aber als System-App gekennzeichnet.
    /// </summary>
    public bool IsUpdatedSystemApp =>
        Kind == PackageKind.System
        && CodePath?.StartsWith("/data/app/", StringComparison.Ordinal) == true;

    /// <summary>Anzeigetext der Herkunft.</summary>
    public string KindText => Kind == PackageKind.System ? Strings.KindSystem : Strings.KindUser;

    /// <summary>Anzeigetext des Zustands (kombiniert mehrere Merkmale).</summary>
    public string StatusText
    {
        get
        {
            var parts = new List<string>(3);

            if (!Installed)
            {
                parts.Add(Strings.StatusRemoved);
            }
            else
            {
                if (Hidden)
                {
                    parts.Add(Strings.StatusHidden);
                }

                if (Suspended)
                {
                    parts.Add(Strings.StatusSuspended);
                }

                if (!IsEnabled)
                {
                    parts.Add(Strings.StatusDisabled);
                }
            }

            if (parts.Count == 0)
            {
                parts.Add(Strings.StatusActive);
            }

            return string.Join(", ", parts);
        }
    }

    /// <summary>Lesbare Gesamtgröße.</summary>
    public string SizeText => TotalSizeBytes > 0 ? Humanize.Bytes(TotalSizeBytes) : Strings.ValueUnknown;

    /// <summary>Lesbares Installationsdatum.</summary>
    public string InstalledText => FirstInstallTime is { } time
        ? time.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.CurrentCulture)
        : Strings.ValueUnknown;

    /// <summary>Lesbarer Installierer.</summary>
    public string InstallerText =>
        string.IsNullOrWhiteSpace(InstallerPackage) ? Strings.ValueUnknown : InstallerPackage!;

    /// <summary>Lesbare Version.</summary>
    public string VersionText =>
        string.IsNullOrWhiteSpace(VersionName) ? Strings.ValueUnknown : VersionName!;

    /// <summary>True, wenn die Bewertung die Bloatware-Schwelle erreicht.</summary>
    public bool IsBloatCandidate => BloatScore >= BloatwareClassifier.BloatThreshold;

    /// <summary>True, wenn die Stufe eine Aktion technisch sperrt (CRITICAL, Abschnitt 6.2).</summary>
    public bool IsLocked => Safety == PackageSafety.Critical;

    /// <summary>Hilfswerte für die farbige Safety-Anzeige in der Tabelle.</summary>
    public bool IsSafe => Safety == PackageSafety.Safe;

    /// <inheritdoc cref="IsSafe" />
    public bool IsCaution => Safety == PackageSafety.Caution;

    /// <inheritdoc cref="IsSafe" />
    public bool IsCritical => Safety == PackageSafety.Critical;

    /// <summary>Anzeigetext der Gefahren-Einstufung.</summary>
    public string SafetyText => Safety switch
    {
        PackageSafety.Safe => Strings.SafetySafe,
        PackageSafety.Critical => Strings.SafetyCritical,
        _ => Strings.SafetyCaution,
    };

    /// <summary>Anzeigetext der Bewertung: Punktzahl bei Kandidaten, sonst ein Strich.</summary>
    public string BloatText => IsBloatCandidate
        ? BloatScore.ToString(CultureInfo.CurrentCulture)
        : Strings.ValueUnknown;

    /// <summary>Alle Begründungen als mehrzeiliger Text (für Tooltips).</summary>
    public string BloatReasonText => BloatReasons.Count == 0
        ? string.Empty
        : string.Join(Environment.NewLine, BloatReasons);

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Meldet die vom Zustand abhängigen Anzeigewerte, wenn sich der Zustand
    /// ändert (z. B. nach einer Aktion am Gerät, S-10).
    /// </summary>
    private void NotifyActionState()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(HasActions));
    }

    private void OnPropertyChanged(string propertyName)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
