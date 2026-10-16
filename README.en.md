# TVClock Modern

<p align="center">
  <img src="assets/icon-512.png" width="128" alt="TVClock Modern">
</p>

> **Note: This project is not affiliated with TVClock or its author in any way.**
> It is an independent open-source project and does not use any of the original code, resources, or trademarks.

A digital clock that sits on your Windows desktop. A general-purpose desktop clock **implemented from scratch using a clean-room approach**.
Built with .NET 8 / WPF. Single exe, resident, low overhead, and designed to resist crashes.

![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue) ![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey)

## Features

- **Widget-like residency**: always on top / borderless / transparent background / hidden from Alt-Tab / single exe
- **Multi-monitor support**: simultaneous display on all monitors, monitor selection, automatic adaptation to monitor configuration changes, Per-Monitor V2 DPI
- **Rich display**: font / size / color / outline / shadow / opacity / free-format time & date formats
- **Per-character rendering**: adjustable character spacing and separator spacing
- **Transition effects**: horizontal flip / vertical flip / slide up / slide down / fade in-out / crossfade
- **Mouse-proximity fade**: becomes transparent when the mouse approaches. A transparency-release key (Ctrl, etc.) makes it temporarily interactive
- **Click-through**: interact with what's beneath the clock (you can't get locked out — the tray icon is always accessible)
- **Position presets**: top-left / top-right / bottom-left / bottom-right / center. Automatically realigns when the digit count changes
- **Settings import / export**: carried as JSON

## Screenshots

**Desktop residency** (an example with outline, character spacing, and separator spacing adjusted)

![](docs/shot-clock.png)

**Settings screen** (changes are applied via "Apply")

![](docs/shot-settings.png)

**Time transition effects** — digits animate in and out every time the seconds change (effect duration 50–500 ms):

| Effect | Animation |
|---|---|
| Horizontal flip | ![](docs/effect-flipx.gif) |
| Vertical flip | ![](docs/effect-flipy.gif) |
| Slide up | ![](docs/effect-slideup.gif) |
| Slide down | ![](docs/effect-slidedown.gif) |
| Fade in-out | ![](docs/effect-fade.gif) |
| Crossfade | ![](docs/effect-crossfade.gif) |

## Crash-Resistant Design

- Triple exception handlers (`Dispatcher` / `AppDomain` / `TaskScheduler`) — UI exceptions are logged and the app continues; on fatal exit it **automatically restarts itself**
- Settings file uses **atomic writes + 3-stage fallback** (main → backup → defaults); corrupted files are quarantined
- **Second-synchronized timer** (wakes once per second), fades are GPU-composited, and proximity monitoring is a mere 60 Hz poll
- Logs: `%LOCALAPPDATA%\TVClockModern\logs\` (auto-rotates 7 days' worth)

## Requirements

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) (not needed for self-contained builds)

## Build

```powershell
# Framework-dependent (~150 KB)
dotnet publish -c Release -o publish

# Self-contained single exe (no runtime required)
dotnet publish -c Release -o publish-sc -p:SelfContained=true
```

## Usage

| Action | Behavior |
|---|---|
| Launch | `publish\TVClockModern.exe` |
| Drag | Move (position is saved automatically) |
| Double-click | Settings screen |
| Right-click | Menu |
| Task tray | All functions (including releasing click-through mode) |
| `--settings` argument | Launch with the settings screen (for automation) |

Settings are saved to `%APPDATA%\TVClockModern\settings.json`.

**See the [user manual](docs/USER_MANUAL.md) for detailed usage.**
(explanations of all settings, a list of format specifiers, multi-monitor behavior, troubleshooting, etc.)

For the change history, see [CHANGELOG.md](CHANGELOG.md).

## Disclaimer (No Warranty, As Is)

This software is provided **as is**. No warranty of any kind is made, whether express or implied, including but not limited to merchantability, fitness for a particular purpose, and non-infringement.

- The author assumes no liability whatsoever for any damage (data loss, malfunction, financial loss, etc.) arising from the use of or inability to use this software
- Due to the nature of resident software, unexpected behavior may occur depending on your environment. Please verify operation before deploying to a critical work environment
- Manage your settings files and logs at your own responsibility

## License

[MIT](LICENSE)

## About the Name

The name "TVClock Modern" exists to indicate that this is an **independent project** inspired by existing desktop clock apps. It is not:

- A fork, modification, or disassembly of the existing TVClock (or any other clock software)
- An affiliation with, endorsement by, or sponsorship of the authors or rights holders of existing software
- Use of any resources (images, icons, code) from existing software
