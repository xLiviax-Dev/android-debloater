; Inno-Setup-Skript fuer Android Debloater (Windows x64)
; Wird von build-release.ps1 aufgerufen und bekommt dabei uebergeben:
;   /DAppVersion=1.0.0   /DSourceDir=<portabler Ordner>   /DOutputDir=<Zielordner>
; Der Installer enthaelt genau dieselben Dateien wie das portable ZIP.

#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#ifndef SourceDir
  #define SourceDir "..\release\portable\AndroidDebloater-Win-x64"
#endif
#ifndef OutputDir
  #define OutputDir "..\release\installer"
#endif

[Setup]
AppId={{6F1D2B8E-4C7A-4B59-9E0A-3D5A7C21B8F4}
AppName=Android Debloater
AppVersion={#AppVersion}
AppPublisher=Android Debloater
DefaultDirName={autopf}\Android Debloater
DefaultGroupName=Android Debloater
DisableProgramGroupPage=yes
UninstallDisplayName=Android Debloater
UninstallDisplayIcon={app}\AndroidDebloater.exe
LicenseFile={#SourceDir}\LICENSE
OutputDir={#OutputDir}
OutputBaseFilename=AndroidDebloater-Setup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Ohne Administrator installierbar (pro Benutzer); der Nutzer kann im Dialog "fuer alle" waehlen.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Android Debloater"; Filename: "{app}\AndroidDebloater.exe"
Name: "{group}\{cm:UninstallProgram,Android Debloater}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Android Debloater"; Filename: "{app}\AndroidDebloater.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\AndroidDebloater.exe"; Description: "{cm:LaunchProgram,Android Debloater}"; Flags: nowait postinstall skipifsilent

; Hinweis: Die Nutzerdaten unter %LOCALAPPDATA%\Android Debloater (Einstellungen,
; Rollback-Profile, Protokoll) bleiben bei der Deinstallation bewusst erhalten.
