# Installing Android Debloater

Android Debloater is a Windows desktop application that talks to an Android
device over ADB (Android Debug Bridge) to list, remove and restore apps —
without root. This document explains how to get it running.

There are two ready-to-use downloads for Windows (see "Option 1" and
"Option 2" below), and you can still build the app from source. How to use the
app is described in [USER-GUIDE.md](USER-GUIDE.md).

## Requirements

- Windows 10 or Windows 11 (64-bit)
- An Android device with **USB debugging** enabled (see the user guide)
- A USB cable to connect the device
- ADB — you do **not** have to install it yourself. The app downloads it for
  you from Google with one click on first start (see "First start" below). It is
  not part of the ZIP or the installer.
- Only when building from source: the **.NET 10 SDK**. The packaged downloads
  are self-contained and need no .NET installation.

## Option 1 — Portable ZIP (no installation)

1. Download `AndroidDebloater-Win-x64.zip` from the releases page.
2. Unpack it anywhere (a USB stick works too).
3. Start `AndroidDebloater.exe` from the unpacked folder:

   ```
   AndroidDebloater-Win-x64\
     AndroidDebloater.exe
     USER-GUIDE.md
     LICENSE
   ```

Nothing is written to Windows itself. ADB is not included: on the first start the
app offers to download it (see "First start"). To remove the app, delete the
folder.

## Option 2 — Installer

1. Download `AndroidDebloater-Setup-<version>.exe` from the releases page.
2. Run it and follow the wizard. It installs for the current user without
   administrator rights (the wizard can also install for all users).
3. Start **Android Debloater** from the Start menu (an optional desktop icon is
   offered in the wizard).

To remove the app, use **Settings → Apps → Installed apps** (or the uninstall
entry in the Start menu folder). Your settings and recovery profiles are kept.

## Option 3 — Run from source

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Get the source code (clone the repository or download it as a ZIP).
3. Open a terminal in the repository root and build the solution:

   ```
   dotnet build AndroidDebloater.sln -c Release
   ```

4. Run the app:

   ```
   dotnet run --project src\AndroidDebloater.App -c Release
   ```

   Alternatively, start the built executable directly:

   ```
   src\AndroidDebloater.App\bin\Release\net10.0\AndroidDebloater.App.exe
   ```

### A note about the runtime (source builds only)

A build from source targets `net10.0` (Windows desktop) and is not self-contained.
Running the produced `AndroidDebloater.App.exe` directly requires the
**.NET 10 Desktop Runtime** to be installed on the machine. If it is not, either
install the runtime or run the app through the SDK host instead:

```
dotnet src\AndroidDebloater.App\bin\Release\net10.0\AndroidDebloater.App.dll
```

## Building the release files

The two downloads are produced by one script from the repository root:

```
.\build-release.ps1
```

It publishes a self-contained single executable, writes the portable ZIP to
`release\portable\` and, if [Inno Setup 6](https://jrsoftware.org/isinfo.php) is
installed, the installer to `release\installer\`. ADB is not bundled by default
(the app downloads it itself). Useful options: `-Version 1.0.1`, `-WithAdb` (also
bundle `platform-tools` next to the executable) and `-SkipInstaller`.

## First start

1. Start the app. On the first launch it looks for an ADB installation in this
   order: next to the app (portable), the `ADB` / `ANDROID_HOME` /
   `ANDROID_SDK_ROOT` environment variables, the system `PATH`, the common SDK
   locations, a `winget` install, and finally ADB that the app itself downloaded.
2. **If ADB is missing**, click **Download ADB...**. The app:
   - reads the version, file name, size and **SHA-1** checksum of the official
     Platform-Tools from Google's SDK manifest,
   - shows them in a confirmation dialog,
   - downloads the versioned ZIP from `dl.google.com`,
   - verifies the SHA-1 checksum before unpacking,
   - installs the files into `%LOCALAPPDATA%\Android Debloater\platform-tools`,
   - verifies the installation with `adb --version`.

   Google does not publish a SHA-256 value for this archive, so the official
   SHA-1 from Google's SDK manifest is the value that is verified. A SHA-256 is
   also calculated and shown for cross-checking, but it is not an official value.
3. Connect your Android device over USB, enable USB debugging on it, and accept
   the "Allow USB debugging?" prompt on the device.
4. Back in the app, the **Device** card should now show your device. Pick it in
   the drop-down; the app reads the device information and scans the package
   list automatically.

## Where the app stores data

Everything the app itself writes lives under:

```
%LOCALAPPDATA%\Android Debloater\
```

| Path | Contents |
| --- | --- |
| `settings.json` | Your settings (language, whitelist, ADB path, expert mode) |
| `platform-tools\` | The ADB the app downloaded, if any |
| `icons\` | Cached app icons and display names |
| `rollback\` | Saved recovery profiles and rescue files |
| `export\` | Exported package lists (CSV / Markdown / HTML) |
| `actions.log` | The action log (every change, with the undo command) |

## Uninstalling

The app itself does not write anything outside its own folder and its data folder, so removing it is simple:

- **Installer version:** uninstall it via Windows (**Settings → Apps**).
- **Portable version / source build:** delete the app folder.

Your own data stays in place in both cases. Optionally delete
`%LOCALAPPDATA%\Android Debloater` to remove your settings,
the icon cache, the downloaded ADB, the saved recovery profiles and the log.

Deleting that folder does **not** change anything on your Android device.

## Troubleshooting

- **"No ADB installation was found on this system."** — Use **Download ADB...**
  or set a path under **Settings → ADB path** (either the `adb` file itself or
  its `platform-tools` folder).
- **The device shows as unauthorized.** — Unlock the device and confirm the
  "Allow USB debugging?" prompt. Enable "Always allow from this computer" to
  avoid repeating it.
- **The device shows as offline.** — Unplug and reconnect the cable, or run
  `adb kill-server` followed by `adb devices`, then click **Refresh**.
- **No device is listed.** — Make sure USB debugging is enabled (Developer
  options), the cable supports data (not charge-only) and the ADB driver is
  available.
- **The app does not start.** — The portable ZIP and the installer need no
  .NET installation. For a source build, install the .NET 10 Desktop Runtime or
  run the app through the SDK host as described above.

## For developers

Run the test suite with the .NET 10 SDK:

```
dotnet test tests\AndroidDebloater.Tests\AndroidDebloater.Tests.csproj
```
