# 🔍 Free Zoom Widget for Xbox Game Bar

> 🇷🇺 [Читать на русском](README.ru.md)

A free, open-source on-screen magnifier and crosshair overlay for Windows, delivered as an Xbox Game Bar widget. A full replacement for the paid "Crosshair Zoom" widget — no time limits, no license keys.

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-blue) ![License: MIT](https://img.shields.io/badge/License-MIT-green)

## Features

| Feature | Description |
|---------|-------------|
| **Screen magnifier** | Captures the monitor and renders a zoomed view inside the widget |
| **Follow cursor** | Zoom follows your mouse in real-time (Lock off) |
| **Lock to centre** | Lock the zoom to the exact centre of your screen — widget can be anywhere |
| **Lock to point** | Lock the zoom to any arbitrary screen point (Lock here) |
| **Crosshair overlay** | 7 presets: Dot, Cross, Plus (gap), Circle, Circle+Dot, T-Shape, Chevron |
| **Crosshair customisation** | Color (6 presets), size, thickness, centre gap |
| **Circular / rectangular** | Clip the zoom to a circle or keep it rectangular |
| **Magnification levels** | 1× through 8× |
| **Hotkey** | Hold or Toggle mode, any single key, works while game is focused |
| **Adaptive performance** | Auto-adjusts frame skip based on render cost |
| **Widget resize** | Slider in settings to resize the zoom window |
| **Fully transparent** | No borders, no tint — just the zoomed image |
| **Pinned overlay** | Pin the widget over your game, works in pinned mode |

## Requirements

- **Windows 10** version 1809 (build 17763) or newer. Windows 11 recommended.
- **Visual Studio 2022** with the **Universal Windows Platform development** workload.
- **Developer Mode** enabled in Windows Settings → For developers.

## Build & Install

```powershell
# Clone
git clone https://github.com/Da3m0nRU/zoom-gamebar-widget.git
cd zoom-gamebar-widget/GameBarUWP

# Open in Visual Studio 2022
start GameBarWidget.sln
```

1. Set platform to **x64**.
2. **Build → Deploy Solution**.
3. Open Xbox Game Bar: `Win+G` → Widget menu → **Zoom**.

### First-time setup

1. **Pin the widget** (push-pin icon in the title bar). This is required for the screen picker to work.
2. Close Game Bar (`Esc` or `Win+G`).
3. Click **Select screen** in the widget and choose your monitor.
4. Done — the widget now magnifies continuously.

## How it works

```
┌─────────────────────────────────────────────────┐
│  Windows.Graphics.Capture (monitor capture)     │
│  ↓                                              │
│  CanvasBitmap → mask widget area → sample rect  │
│  ↓                                              │
│  Win2D CanvasSwapChain → draw zoomed image      │
│  ↓                                              │
│  Draw crosshair overlay on top                  │
│  ↓                                              │
│  Present to Game Bar widget window              │
└─────────────────────────────────────────────────┘
```

- **No recursion**: the widget's own screen area is painted black in the captured frame before sampling, so the zoom never includes itself.
- **Hotkey**: uses `GetAsyncKeyState` with the `inputForegroundObservation` restricted capability to read keys even when a game has focus.
- **Settings**: stored in `ApplicationData.LocalSettings`, shared between the zoom widget and the settings widget via sequence counters.

## Settings

Open settings via the gear icon (⚙) in the widget title bar.

### Zoom section
- **Magnification** — 1× to 8×
- **Hotkey** — bind any key, Hold or Toggle mode
- **Lock Zoom Area** — Lock center (screen centre) / Lock here (cursor point) / Off (follow cursor)
- **Circular Zoom** — circle or rectangle clip
- **Adaptive Performance** — auto frame-skip
- **Frame Skip** — manual 0–10

### Crosshair section
- **Enable** — on/off
- **Style** — Dot, Cross, Plus, Circle, Circle+Dot, T-Shape, Chevron
- **Color** — Green, Red, Cyan, Yellow, White, Magenta
- **Size** — 4–200 px
- **Thickness** — 1–10 px
- **Gap** — 0–50 px (for Plus / CircleDot styles)

### Widget Window section
- **Widget Size** — resize slider (200–1000 px)
- **Centralize window** — centre the widget on screen

## Known limitations

- **Screen picker required on Windows 10.** The system shows a one-time picker dialog to grant capture access. On Windows 11 22H2+ this can be skipped with programmatic capture.
- **Yellow capture border on Windows 10.** The system draws a yellow border around the captured monitor. On Windows 11 this is suppressed via `IsBorderRequired = false`.
- **Widget must be pinned** before clicking Select screen. Game Bar's overlay mode blocks the system picker.
- **Single monitor.** The widget captures the monitor it was on when the picker was shown. Multi-monitor switching requires re-picking.

## Publishing

### GitHub (recommended for community)
Fork it, improve it, share it. MIT license — do whatever you want.

### Microsoft Store / Game Bar Widget Store
You can publish to the Store. Notes:
- `inputForegroundObservation` and `graphicsCaptureProgrammatic` are restricted capabilities — Microsoft will ask for justification during submission. "On-screen magnifier for accessibility" is a valid reason.
- Remove `AppListEntry="none"` from the manifest if you want a Start Menu entry.
- Sign with a real certificate (not the dev temp key).
- The widget will automatically appear in Game Bar's Widget Store once published.

## Project structure

```
App.xaml[.cs]              Widget activation routing
MainPage.xaml[.cs]         Zoom renderer + crosshair drawing
SettingsPage.xaml[.cs]     Settings widget UI
ErrorPage.xaml[.cs]        Fallback error display
CaptureHelper.cs           Interop (HWND, WDA, monitor, cursor, capture)
ZoomSettings.cs            Persisted settings (LocalSettings)
Package.appxmanifest       Two widget extensions (Standard + Settings)
```

## Contributing

PRs welcome. Ideas for future work:
- Custom crosshair image (PNG overlay)
- Scope image overlay (like the paid version)
- Multi-monitor switching without re-picking
- Magnification hotkey shortcuts (Alt+1 through Alt+8)
- Color picker instead of preset buttons

## License

MIT — free for everyone. No time limits. No payments. For the working people. 🤝
