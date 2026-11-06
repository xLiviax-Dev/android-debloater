# Android Debloater

A Windows desktop app (Linux and macOS are planned) that helps you clean up an
Android device over ADB, **without root**. It lists the installed apps and
helps you remove or disable unwanted preinstalled software (bloatware) in a
way that is transparent and reversible.

## Download

Windows 10 / 11 (64-bit), no .NET installation needed. Two downloads are offered
on the releases page; pick one:

- **Portable ZIP** — unpack and start `AndroidDebloater.exe`, nothing is installed.
- **Installer** — `AndroidDebloater-Setup-<version>.exe` with a Start menu entry
  and a normal uninstall.

ADB is not included: on the first start the app offers to download it from
Google with one click. Details are in [INSTALL.md](INSTALL.md), how to use the app
is in [USER-GUIDE.md](USER-GUIDE.md). The downloads are not code-signed, so
Windows may show a warning on the first start.

## Features

- List installed apps (system and user) with sizes and status
- Classify packages as safe / caution / critical, based on real signals (no guessing)
- Remove apps reversibly, or disable them
- Restore removed apps, or undo all changes of a session
- Export the list (HTML / CSV / Markdown)
- Detect ADB automatically; download it with one click if it is missing
- Light and dark mode

Linux and macOS are planned.

## Requirements

- Windows 10 / 11 (64-bit); Linux/macOS planned
- An Android device with USB debugging enabled
- ADB (the app downloads it for you)

## Disclaimer

Not affiliated with, endorsed by, or a product of Google LLC.
"Android" is a trademark of Google LLC.
Use at your own risk: removing the wrong package can break your device
(up to a factory reset).

## License

MIT — see [LICENSE](LICENSE).
