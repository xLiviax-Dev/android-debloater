# Android Debloater — User Guide

Android Debloater is a Windows desktop app that helps you clean up an Android
device over ADB, **without root**. It lists the installed apps, marks the ones
that look like bloatware, and removes or disables them in a way that is
transparent and reversible.

This guide explains the screen, the safety model and the day-to-day workflow.
For setup and installation, see [INSTALL.md](INSTALL.md).

> **Safety first:** removing the wrong package can make your device unstable or
> unbootable. Every change in this app is reversible, and every change is logged
> — read the section "The safety model" before you start.

---

## 1. Before you start

1. On the Android device, enable **Developer options** (tap *Build number* seven
   times under *About phone*) and then enable **USB debugging**.
2. Connect the device to the PC with a USB cable.
3. When the device asks *"Allow USB debugging?"*, tap **Allow**. Selecting
   *Always allow from this computer* avoids repeating this step.
4. Start Android Debloater and make sure the **ADB** card shows a version.

If ADB is not found, use **Download ADB...** in the ADB card (see
[INSTALL.md](INSTALL.md), "First start").

---

## 2. The main window

The window is split into three parts: the **ADB** card and the **Device** card
on top (side by side), and the **Packages** card below, which fills the rest of
the window.

### Top bar

The top bar shows the app name and two kinds of status chips: **ADB ready** or
**ADB missing**, and the model of the connected device or **No device**. On the
right are three buttons: the **light/dark switch** (moon icon), the **info**
button (this guide and About) and **Settings**. The look you choose with the
switch is remembered; until you use it, the app follows the Windows setting.

### ADB card

- Shows the detected ADB **version**, its **path** and where it was found
  (**source**, e.g. system PATH or "downloaded by Android Debloater").
- **Check again** — searches for ADB again.
- **Download ADB...** — appears only when no ADB was found, and starts the
  guided download from Google.

### Device card

- The drop-down lists the connected device(s). Picking one loads its details and
  starts the package scan automatically.
- **Refresh** — re-reads the device list.
- Below the drop-down, the app shows **model**, **Android version (SDK)**,
  **serial number** and **build** of the selected device.
- If nothing is connected, the card explains what to do. Unauthorized and
  offline devices are reported with a hint on how to fix the state.

### Packages card

The header row contains:

- the number of packages currently shown,
- **Export** — writes the visible list as CSV, Markdown or HTML (see section 7),
- **Show log** — shows the action log (see section 6),
- **Open folder** — opens the folder that holds the log and recovery files,
- **Reload** — rescans the packages of the selected device.

Below that is the **search box** (filters by app name or package name), a row of
**filter buttons** (*All*, *Apps*, *Games*, *Bloatware*, *Privileged*) and the
**category tabs**, each with the number of packages in it. The numbers do not
change with the search or the filter:

| Tab | What it shows |
| --- | --- |
| **All** | Every package, system and user |
| **User apps & games** | Apps the user installed (Play Store and others) |
| **System apps & games** | Preinstalled system apps that have a launcher icon |
| **System services** | System components without a launcher icon |
| **Disabled** | Packages that are installed but turned off |
| **Removed** | Packages whose app was removed but whose data was kept |

The *Apps* and *Games* filters use the game flag the device itself keeps
(`dumpsys game`), so a game stays a game even when its package name does not say
so. Games found this way are remembered locally, so a game you have *removed*
also stays a game. In the **Removed** tab the filter only offers *All* — apps and
games are listed together there, because the device no longer reports whether a
removed package was a game. Every tab shows the same table with the rows
pre-filtered for that category.

After a scan the app starts loading **icons and display names** in the
background. This only reads data from the device, is cancellable, and the
results are cached locally. You can also click the **icon cell** of a single row
to load it on demand.

### Table columns

| Column | Meaning |
| --- | --- |
| **Icon** | App icon; a coloured letter is shown as a fallback |
| **Package** | Display name (verified from the APK) with the package name below it |
| **Type** | System or User |
| **Status** | Active, Disabled, Hidden, Suspended or Removed |
| **Safety** | Safe, Caution or Critical (see section 3) |
| **Bloat** | Bloatware score (0–100) as a bar with the number, shown for bloatware candidates; hover for the reasons |
| **Size** | Size on disk |
| **Version** | Installed version |
| **Installed on** | Install path / date information |
| **Action** | The **Action ▾** menu for this row: every action that fits right now (section 4) |
| **Installer** | Which installer placed the app, if known |

---

## 3. The safety model

Every package is classified, using real signals from the device (never guessed
from the package name alone):

- **Safe** — a normal, removable app (for example an OEM promo app). A single
  click on **Run action** is enough — also when its data is deleted for good.
- **Caution** — potentially important. It asks for an extra confirmation: you
  must type the package name by hand.
- **Critical** — part of the system core (for example the System UI, the phone
  app, the package installer). **Locked** in normal operation. See section 8
  for the expert mode.

Two additional protection layers apply:

- The **whitelist** protects packages that should never be changed (by default
  the Google Play Store, Google Play services and the Family Link agent). You
  can edit this list in the settings (section 8).
- The **Bloat** score (0–100) is a hint, not a rule: a high score means the app
  shows typical bloatware signals. Hover over the value to see which signals
  matched.

If a package is protected, critical, or nothing needs to be done, the
**Action ▾** menu stays empty.

---

## 4. Changing an app

Each row has one **Action ▾** menu that lists *every* action which fits the app
right now. What appears depends on the app's origin (system or user) and its
current state:

| Action | Appears when | Command | Undo |
| --- | --- | --- | --- |
| **Disable** | installed and active | `pm disable-user --user 0` | `pm enable --user 0` |
| **Enable** | installed and disabled | `pm enable --user 0` | `pm disable-user --user 0` |
| **Remove** | installed *system* app | `pm uninstall --user 0 -k` (uninstall for the user, keep data) | `cmd package install-existing --user 0` |
| **Uninstall (permanent)** | installed *user* app | `pm uninstall --user 0` (app and data deleted) | none — reinstall from the Play Store |
| **Uninstall (keep data)** | installed *user* app | `pm uninstall --user 0 -k` (app deleted, data kept) | none — reinstall from the Play Store |
| **Restore** | *system* app that was removed | `cmd package install-existing --user 0` | `pm uninstall --user 0 -k` |
| **Delete data** | *user* app that was removed (Removed tab) | `pm uninstall PACKAGE` (clears the kept data for good) | none (section 3, S-21) |

To change an app:

1. Find the row and open its **Action ▾** menu, then pick the action you want.
2. A **confirmation dialog** opens. It is a dry run: it shows
   - the **target device** (model, serial, Android/SDK) — so you always know
     which device you are changing,
   - the **exact command** that will be sent,
   - the **undo command** (or the note that there is none),
   - warnings (for example that removing a system app frees almost no storage,
     since the APK stays in the system partition).
3. For **Caution** (and, in expert mode, **Critical**) packages you must type the
   package name to unlock the **Run action** button. For **Safe** packages a
   single click is enough — also for a permanent action such as **Delete data**.
4. Confirm. The app runs the command, re-reads the new state, and records the
   change.

**How it comes back.** **Remove** uninstalls a *system* app for the user only:
the APK is not deleted — it is hidden for your account, and the app's data is
kept (the `-k` flag). It can be brought back any time with **Restore**.
**Disable** is fully reversible with **Enable**. **Uninstall (permanent)**,
**Uninstall (keep data)** and **Delete data** cannot be undone over ADB — the
dialog says so, and the only way back is a fresh install from the Play Store.

---

## 5. Restoring an app

Both ways back live in the same **Action ▾** menu:

- For a removed *system* app, pick **Restore** — it runs
  `cmd package install-existing --user 0`.
- For a disabled app, pick **Enable** — it runs `pm enable --user 0`.

The same confirmation dialog appears before the command runs.

---

## 6. Undo all changes (rollback) and the log

While a scan is open, every change you make in this session is recorded in a
**recovery profile** (with the exact undo command) *before* it is executed.

- When there is at least one recorded change, a bar appears: *"N changes this
  session"* with two buttons:
  - **Undo all changes** — undoes every recorded change **in reverse order**,
    re-reads the state, and removes the successfully restored entries. Clicking
    it again therefore does not repeat anything.
  - **Save rescue file** — writes a plain-text file with all undo commands, so
    you can copy them manually if needed.
- **Show log** — shows the action log (every change and every undo, with the
  before/after state). **Open folder** opens the folder that contains the log,
  the recovery profiles and the rescue files.

The log (`actions.log`) and the recovery files are stored in
`%LOCALAPPDATA%\Android Debloater` (see [INSTALL.md](INSTALL.md)).

---

## 7. Exporting the list

Click **Export** and choose a format:

- **CSV** — for spreadsheets.
- **Markdown** — for documents and pull requests.
- **HTML** — a self-contained page with the bloatware rows highlighted.

The export contains exactly the rows that are currently **visible** (after
search, filter and tab), with a header line (device, Android version, creation
time). Files are written to `%LOCALAPPDATA%\Android Debloater\export` and the
path is shown in the status bar.

---

## 8. Settings

Open the settings with the **Settings** button in the top-right corner.

- **Language** — only English is available in this version; additional
  languages are prepared.
- **Whitelist** — the list of protected packages. Add, remove or restore the
  default entries. Packages on this list are never changed.
- **ADB path** — enter the full path to `adb`, or to its `platform-tools`
  folder, to use a specific ADB. Leave it empty (or click *Detect
  automatically*) to let the app find ADB itself.
- **Icon cache** — shows how much is cached and lets you clear it.
- **Game list** — the packages the device reported as games, remembered locally so
  a removed game keeps its game status. Its own button resets this list (the icon
  cache is *not* touched by it).
- **Expert mode** — see below.

### Expert mode

By default, **Critical** packages are locked (section 3). Expert mode is the only
way to unlock them, and turning it on is deliberately hard:

1. Click **Enable expert mode...**.
2. A warning explains that changing critical packages can make the device
   unstable or unbootable. Confirm to continue.
3. A **second, separate confirmation** requires you to tick a box that states you
   accept the risk.

Only then does expert mode turn on. While it is on, a **banner** stays visible at
the top of the main window as a reminder, and critical packages become
changeable (they still require typing the package name to confirm). The
whitelist remains protected even in expert mode. Turning expert mode **off**
restores the hard lock on critical packages immediately.

---

## 9. A safe way to work

1. Plug in the device and let the app scan it.
2. Start with the **Bloatware** filter and skim the list. Check an app before you
   touch it — a familiar name or the display name is a good hint, but a package
   name alone can be misleading.
3. Change **one app at a time**. After each change, re-read the row's status.
4. Watch the **rollback bar**. If something misbehaves, click **Undo all
   changes**.
5. Save a **rescue file** before larger clean-ups, so you always have the undo
   commands at hand.
6. Leave Critical packages alone unless you are certain. If you do use expert
   mode, turn it back off when you are done.

---

## 10. Troubleshooting

- **No packages appear.** — No device is selected, or the device is
  unauthorized/offline. Check the Device card and the ADB card.
- **The device is not detected.** — See [INSTALL.md](INSTALL.md),
  "Troubleshooting".
- **An app cannot be changed.** — It is protected (on the whitelist), critical,
  or already in the desired state. The status bar explains which.
- **A change was refused.** — If removing a system app is refused by the device,
  the app falls back to disabling it for the user (its undo command becomes
  `pm enable`). You can see this in the dialog and in the log.
- **The state could not be confirmed.** — The command ran, but the app could not
  read the new state back. Re-run **Reload**, and check the log before making
  further changes.
