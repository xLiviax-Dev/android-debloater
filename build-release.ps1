<#
.SYNOPSIS
    Baut die Release-Dateien von Android Debloater (Windows x64):
    ein portables ZIP und - falls Inno Setup vorhanden ist - einen Installer.

.DESCRIPTION
    Schritte:
      1. dotnet publish: selbstaendige EXE (self-contained, .NET ist enthalten).
      2. Portable-Ordner: AndroidDebloater.exe + platform-tools\ (ADB) + Doku.
      3. ZIP daraus erzeugen.
      4. Installer (Inno Setup) aus demselben Ordner erzeugen.

    Ergebnis (Ordner release\):
      release\portable\AndroidDebloater-Win-x64.zip
      release\installer\AndroidDebloater-Setup-<Version>.exe

.PARAMETER Version
    Versionsnummer fuer Installer und Dateinamen (Standard 1.0.0).

.PARAMETER PlatformTools
    Nur mit -WithAdb: Ordner mit adb.exe, der mitgeliefert wird. Standard: der Ordner,
    in den die App ADB selbst herunterlaedt (%LOCALAPPDATA%\Android Debloater\platform-tools).

.PARAMETER WithAdb
    ADB mitliefern (Ordner platform-tools neben der EXE). Standard: NICHT mitliefern;
    die App laedt ADB beim ersten Start auf Klick selbst von Google.

.PARAMETER SkipInstaller
    Nur das portable ZIP bauen.

.EXAMPLE
    .\build-release.ps1
    .\build-release.ps1 -Version 1.0.1
    .\build-release.ps1 -WithAdb
#>
param(
    [string]$Version = '1.0.0',
    [string]$PlatformTools = (Join-Path $env:LOCALAPPDATA 'Android Debloater\platform-tools'),
    [switch]$WithAdb,
    [switch]$SkipInstaller
)

$ErrorActionPreference = 'Stop'

$root = $PSScriptRoot
$project = Join-Path $root 'src\AndroidDebloater.App\AndroidDebloater.App.csproj'
$releaseDir = Join-Path $root 'release'
$publishDir = Join-Path $releaseDir '_publish'
$portableRoot = Join-Path $releaseDir 'portable'
$portableDir = Join-Path $portableRoot 'AndroidDebloater-Win-x64'
$zipPath = Join-Path $portableRoot 'AndroidDebloater-Win-x64.zip'
$installerDir = Join-Path $releaseDir 'installer'
$issPath = Join-Path $root 'installer\AndroidDebloater.iss'

# Portables SDK im Projekt bevorzugen, sonst das System-dotnet.
$dotnet = Join-Path $root '.dotnet\dotnet.exe'
if (-not (Test-Path $dotnet)) { $dotnet = 'dotnet' }

function Step($text) { Write-Host ""; Write-Host "== $text" -ForegroundColor Cyan }

# ADB nur bei -WithAdb vorab pruefen, damit nicht erst nach dem Build abgebrochen wird.
if ($WithAdb) {
    $adbExe = Join-Path $PlatformTools 'adb.exe'
    if (-not (Test-Path $adbExe)) {
        throw "adb.exe nicht gefunden in '$PlatformTools'. Starte die App einmal und lade ADB per Klick herunter oder gib mit -PlatformTools einen anderen Ordner an."
    }
}

Step 'Alten Release-Ordner leeren'
if (Test-Path $releaseDir) { Remove-Item $releaseDir -Recurse -Force }
New-Item -ItemType Directory -Path $publishDir, $portableDir, $installerDir | Out-Null

Step 'Release bauen (self-contained, eine EXE)'
& $dotnet publish $project -c Release -r win-x64 --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=none -p:DebugSymbols=false `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw 'dotnet publish ist fehlgeschlagen.' }

Step 'Portable-Ordner zusammenstellen'
Copy-Item (Join-Path $publishDir 'AndroidDebloater.App.exe') (Join-Path $portableDir 'AndroidDebloater.exe')
Copy-Item (Join-Path $root 'USER-GUIDE.md') $portableDir
Copy-Item (Join-Path $root 'LICENSE') $portableDir
if ($WithAdb) {
    Copy-Item $PlatformTools (Join-Path $portableDir 'platform-tools') -Recurse
}

Step 'ZIP erzeugen'
Compress-Archive -Path $portableDir -DestinationPath $zipPath -CompressionLevel Optimal

if (-not $SkipInstaller) {
    Step 'Installer erzeugen (Inno Setup)'
    $iscc = $null
    $candidates = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    )
    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) { $iscc = $candidate; break }
    }
    if (-not $iscc) {
        $cmd = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($cmd) { $iscc = $cmd.Source }
    }

    if ($iscc) {
        & $iscc "/DAppVersion=$Version" "/DSourceDir=$portableDir" "/DOutputDir=$installerDir" $issPath
        if ($LASTEXITCODE -ne 0) { throw 'Inno Setup ist fehlgeschlagen.' }
    }
    else {
        Write-Warning 'Inno Setup 6 nicht gefunden - Installer uebersprungen. Installation: https://jrsoftware.org/isinfo.php'
    }
}

Remove-Item $publishDir -Recurse -Force

Step 'Fertig'
Get-ChildItem $zipPath, (Join-Path $installerDir '*.exe') -ErrorAction SilentlyContinue |
    ForEach-Object { '{0}  ({1:N1} MB)' -f $_.FullName, ($_.Length / 1MB) }
