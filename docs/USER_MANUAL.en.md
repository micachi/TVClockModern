# TVClock Modern User Manual

Covers version 1.3.0

---

## Table of Contents

1. [Introduction](#1-introduction)
2. [Installation and Startup](#2-installation-and-startup)
3. [Basic Operations](#3-basic-operations)
4. [Settings Reference (Format/Text/Decoration Tabs)](#4-settings-reference)
5. [Settings Reference (Behavior Tab)](#5-settings-reference-behavior-tab)
6. [Date and Time Format Specifiers](#6-date-and-time-format-specifiers)
7. [Multi-Monitor and Display Position](#7-multi-monitor-and-display-position)
8. [Settings Backup (Export/Import)](#8-settings-backup)
9. [Resetting Settings](#9-resetting-settings)
10. [Troubleshooting](#10-troubleshooting)

---

## 1. Introduction

TVClock Modern is a digital clock that can be displayed permanently on the Windows desktop.
It runs as an always-on-top, borderless, transparent-background window, with fine-grained customization of its appearance and behavior.

### System Requirements

| Item | Requirement |
|---|---|
| OS | Windows 10 / 11 (x64) |
| Runtime | .NET 8 Desktop Runtime (not required for the self-contained exe) |

### Limitations

- This software is provided **as is, with no warranty of any kind** (see the disclaimer in the README for details)
- Multiple simultaneous instances are not supported (launching a second copy shows a message and exits)

---

## 2. Installation and Startup

### Installation

There is no installer. Simply place the distributed `TVClockModern.exe` in any folder to use it.
(The registry is only written when "Start automatically" is enabled.)

### Startup

- Double-click the exe and the clock appears on the desktop, residing in the task tray
- Position and settings are saved automatically and restored on the next launch

### Exit

- Right-click the clock → **Exit**
- Or from the task tray icon → **Exit**

### Start automatically when Windows starts

Settings → Behavior tab → check **"Start automatically when Windows starts"** and apply.
(Registers itself in `HKCU\...\Run`. To remove it, uncheck and apply.)

---

## 3. Basic Operations

| Action | Result |
|---|---|
| Drag the clock | Move (position is saved immediately) |
| Double-click the clock | Opens the settings screen |
| Right-click the clock | Menu (Settings / Always on top / Click-through / Exit) |
| Double-click the task tray icon | Opens the settings screen |
| Right-click the task tray icon | Full menu |

### While Click-through is enabled

When "Click-through" is enabled, the clock's area can no longer be clicked (you interact with windows underneath).
In that case, **all operations are available from the task tray icon**.

Also, **while the transparency-bypass key (default: Ctrl) is held down**:

- The clock is shown fully opaque and becomes clickable
- Right-clicking shows the menu
- Releasing the key returns to the previous state (semi-transparent + click-through)

### Multiple Monitors

When "Show on all monitors simultaneously" is enabled, one clock is displayed per monitor.
Operations are the same in this case (settings are shared across all clocks).

---

## 4. Settings Reference (Format/Text/Decoration Tabs)

### Time Format

You can specify the time format freely. Main examples:

| Format | Example |
|---|---|
| `HH:mm` | 09:05 |
| `HH:mm:ss` | 09:05:33 |
| `h:mm` | 9:05 (no leading zero) |
| `hh:mm tt` | 09:05 AM |
| `H時m分` | 9時5分 |

See [6. Date and Time Format Specifiers](#6-date-and-time-format-specifiers) for details on format specifiers.

### Date Format / Show Date

- Leave blank or turn "Show Date" off to hide the date
- Example: `yyyy/MM/dd (ddd)` → `2026/10/15 (Thu)`

### Font / Font Size

Select from fonts installed on the system. Size range is 12–200.

### Text Color

Specified in the color picker dialog.

### Letter Spacing / Separator Spacing

- **Letter spacing**: gap between digits (0–50)
- **Separator spacing**: padding around separators such as ":" (0–200)
- Monospace font + letter spacing 0 is the most compact

### Time Change Effect / Effect Duration

Animation when the seconds or minutes change.

| Effect | Motion |
|---|---|
| None | Instant switch |
| Rotate horizontally | Old digits squash on the vertical axis, new digits unfold |
| Rotate vertically | Flip on the horizontal axis |
| Slide up | Old digits exit upward, new digits rise from below (slot-machine style) |
| Slide down | Slot machine in the reverse direction |
| Fade in/out | Old digits disappear, then new ones appear |
| Cross-fade | Overlap and cross-fade |

Effect duration is 50–500 ms. Longer is flashier; shorter is snappier.

### Text Outline

Adds an outline of a specified color around the text (effective for readability on light wallpapers).
Color and thickness (0.5–10) can be specified.

### Shadow

Adds a shadow to the text. Size (blur radius 0–40) and opacity (0–1) can be specified.

### Window Opacity (Normal)

Overall opacity of the clock. 0.2–1.0.
When "Mouse proximity fade" is used, this is also the opacity **while the mouse is away**.

(Found in the "When mouse approaches" section of the Behavior tab.)

---

## 5. Settings Reference (Behavior Tab)

### Monitor to Display On

- **Auto (current position)**: shows at the saved position (traditional behavior)
- **Specific monitor**: places it in the work area of the selected monitor (excluding the taskbar)

### Show on All Monitors Simultaneously

When enabled, **one clock is displayed per monitor**.

- Each clock's position is remembered per monitor
- Automatically follows monitors being added, removed, or disconnected
- The "Monitor to Display On" setting is ignored

### Always on Top

When off, the clock can be hidden behind other windows.

### Click-through / Transparency-Bypass Key

- Click-through: passes mouse operations in the clock's area through to windows below
- While the transparency-bypass key (Ctrl / Alt / Shift / None) is held down:
  - Click-through is temporarily disabled and the clock is clickable
  - Semi-transparency is cancelled and the clock is fully visible
  - The right-click menu is also available

### Start automatically when Windows starts

As described above. Registers the current exe path in the registry.
**If you move the exe, check this again and apply.**

### Check for updates on startup

When checked, the app queries GitHub Releases for a new version at startup (at most once every 24 hours).
When a new version is found, a tray notification appears and it is also shown on the "About" tab of the settings window.
Clicking the notification or the button on the About tab opens the release page (download). Nothing is downloaded automatically.
Offline starts fail silently — no error is shown — and no telemetry is sent.

### Language

Selects the user interface language (menus and settings window): 日本語 / English / 简体中文 / 한국어.
The default, "Auto (follow system)", follows the Windows display language.
Changes apply to all windows immediately. Date and weekday names follow the Windows regional format.


### When Mouse Approaches

| Item | Description |
|---|---|
| Enabled | Turns the feature on |
| Sensitivity (distance px) | Reacts when the cursor comes within this distance (10–400) |
| Opacity when close | Opacity while the mouse is near (0.1 is nearly transparent) |
| Response speed | 1 (slow, 1 s) – 10 (fast, 30 ms) |
| Fade curve | Linear / Ease-out / Ease-in |

- **While the transparency-bypass key is held down, the clock is shown fully opaque, taking priority over proximity settings**
- Proximity detection polls 60 times per second, with negligible CPU impact

---

## 6. Date and Time Format Specifiers

A list of commonly used specifiers (.NET standard formats).

### Time

| Specifier | Meaning | Example (2026-10-15 21:05:09) |
|---|---|---|
| `HH` | Hour (24-hour, 2 digits) | 21 |
| `H` | Hour (24-hour, no leading zero) | 21 |
| `hh` | Hour (12-hour, 2 digits) | 09 |
| `h` | Hour (12-hour, no leading zero) | 9 |
| `mm` | Minute (2 digits) | 05 |
| `ss` | Second (2 digits) | 09 |
| `tt` | AM/PM | PM |

### Date

| Specifier | Meaning | Example |
|---|---|---|
| `yyyy` | Year (4 digits) | 2026 |
| `yy` | Year (2 digits) | 26 |
| `MM` | Month (2 digits) | 10 |
| `M` | Month (no leading zero) | 10 |
| `dd` | Day (2 digits) | 15 |
| `ddd` | Day of week (abbreviated) | Thu |
| `dddd` | Day of week (full) | Thursday |

### Combination Examples

| Format | Example |
|---|---|
| `yyyy年M月d日(ddd)` | 2026年10月15日(木) |
| `M/d ddd` | 10/15 Thu |
| `yyyy-MM-dd` | 2026-10-15 |
| `HH:mm:ss` | 21:05:09 |

---

## 7. Multi-Monitor and Display Position

### How Position Is Remembered

- The clock's position is stored as a **relative offset from the top-left of the monitor's work area**
- Therefore, even if the monitor's resolution changes or monitors are rearranged, the relative position is preserved
- Saved immediately on drag end, and also auto-saved every minute (generally restored even after a forced exit)

### Position Presets

Use the **Top-left / Top-right / Bottom-left / Bottom-right / Center** buttons on the Behavior tab to align instantly.

- Each clock moves relative to **the monitor it is displayed on**
- After using a preset, if the size changes due to digit-count changes, clocks are **automatically re-aligned to avoid overflowing**
- If you drag without using a preset, the dragged position takes priority

### When a Monitor Is Connected / Disconnected

- When a monitor is disconnected, clocks on it automatically relocate to a visible position
- When reconnected, they automatically return to their remembered positions
- When "Show on all monitors simultaneously" is enabled, clocks are automatically added or removed as monitors change

---

## 8. Settings Backup

From "Settings Transfer" at the bottom of the settings screen:

- **Export**: saves the current settings to a JSON file (for carrying to other PCs)
- **Import**: loads settings from a JSON file and applies them immediately
The Export/Import buttons live on the About tab of the settings window. The About tab also shows the current version, and offers a Check-for-updates button (with a tray notification when a new version is found) and a button to open the release page.

Exported files can also be edited in a text editor (if corrupted, import falls back to defaults).

---

## 9. Resetting Settings

1. Exit TVClock Modern
2. Delete (or rename) `settings.json` in the following folder
   ```
   %APPDATA%\TVClockModern\settings.json
   ```
3. Launch again and it will start with defaults

> If the settings file is corrupted, it is automatically restored from backup (`settings.bak`);
> if that also fails, it starts with defaults. The corrupted file is kept as `settings.json.corrupt-<date-time>`.

---

## 10. Troubleshooting

### The clock disappeared from the screen

- Right after changing the monitor configuration, automatic correction kicks in. Wait a few seconds, or open the settings screen (from the task tray) and press "Apply"
- If "Monitor to Display On" is specified, it moves automatically to the center of that monitor

### Cannot click (while click-through is enabled)

- Task tray icon → uncheck "Click-through"
- Or operate **while holding Ctrl** (transparency-bypass key)

### Cannot launch a second instance

- This is by design. If already running, a message is shown and it exits

### Position / settings are reverted

- Check that `%APPDATA%\TVClockModern\settings.json` is saved correctly
- If you moved the exe, re-check "Start automatically when Windows starts" and apply

### Font is not applied / garbled characters

- Check that the font name matches exactly (copy-paste is recommended)

### Checking Logs

```
%LOCALAPPDATA%\TVClockModern\logs\app-YYYYMMDD.log
```

Logs are retained for 7 days. When reporting a bug, please attach this file.

---

*TVClock Modern is provided as is. See the disclaimer in the README for details.*
