# 🔍 Free Zoom Widget for Xbox Game Bar

> 🇷🇺 [Читать на русском](README.ru.md)

A free, open-source screen magnifier and crosshair overlay that runs as an Xbox Game Bar widget. Works on top of any fullscreen game. No time limits, no license, no payments — unlike the paid alternatives.

![Windows 10/11](https://img.shields.io/badge/Windows-10%20%7C%2011-blue) ![License: MIT](https://img.shields.io/badge/License-MIT-green)

## What it does

- **Magnifies** a portion of your screen (1×–8×) and displays it in a pinned widget overlay.
- **Follows your cursor** or locks to a fixed point (e.g. screen centre — useful for sniping).
- **Draws a crosshair** on top of the zoomed image with 7 shape presets, adjustable color/size/thickness.
- **Works in fullscreen games** — the widget is part of Xbox Game Bar, which renders on top of DirectX/Vulkan apps.
- **Hotkey support** — hold or toggle a key to activate the zoom only when you need it.

## Quick start

### Option 1: Download a release
Go to [Releases](../../releases), download the `.appx` package, and sideload it (requires Developer Mode enabled in Windows Settings).

### Option 2: Build from source

Requirements:
- Windows 10 (1809+) or Windows 11
- Visual Studio 2022 with **Universal Windows Platform development** workload
- Developer Mode enabled

```
git clone https://github.com/user/zoom-gamebar-widget.git
cd zoom-gamebar-widget/GameBarUWP
```

Open `GameBarWidget.sln` in Visual Studio → set platform to **x64** → **Build → Deploy Solution**.

### First launch

1. Press `Win+G` to open Xbox Game Bar.
2. Find **Zoom** in the widget menu and open it.
3. **Pin the widget** (📌 icon in the title bar) — this is required.
4. Close Game Bar (press `Esc`).
5. Click **Select screen** in the widget and pick your monitor.
6. Done. The widget is now magnifying.

## Settings

Click the ⚙ gear icon in the widget title bar.

**Zoom**
| Setting | What it does |
|---------|-------------|
| Magnification | Zoom level (1×–8×) |
| Hotkey | Key that activates/deactivates the zoom (Hold or Toggle mode) |
| Lock Zoom Area | Off = follow cursor. Lock center = always show screen centre. Lock here = lock to cursor position at the moment you click |
| Circular Zoom | Circle or rectangle clip |
| Adaptive Performance | Auto frame-skip when GPU is busy |
| Frame Skip | Manual frame skip (0–10) |

**Crosshair**
| Setting | What it does |
|---------|-------------|
| Enable | Show/hide the crosshair |
| Style | Dot, Cross, Plus (gap), Circle, Circle+Dot, T-Shape, Chevron |
| Color | Green, Red, Cyan, Yellow, White, Magenta |
| Size | Crosshair radius (4–200 px) |
| Thickness | Line width (1–10 px) |
| Gap | Centre gap for Plus/CircleDot styles (0–50 px) |

**Widget Window**
| Setting | What it does |
|---------|-------------|
| Widget Size | Resize the zoom window |
| Centralize window | Move the widget to screen centre |

## How it works (for developers)

- Screen capture via `Windows.Graphics.Capture` API.
- Rendering via Win2D (`CanvasSwapChain`).
- The widget masks its own screen area in the captured frame to prevent infinite recursion.
- Hotkey reads via `GetAsyncKeyState` + `inputForegroundObservation` capability.
- Settings shared between zoom widget and settings widget via `ApplicationData.LocalSettings`.

## Known limitations

- **Widget must be pinned** before selecting a screen. Game Bar's overlay mode blocks the system picker.
- **Yellow capture border on Windows 10** — a system-level indicator that can't be removed. Not present on Windows 11.
- **Single monitor** — captures the monitor selected during setup. Switch by re-picking.

## Contributing

PRs and issues welcome. Some ideas:
- Custom crosshair image (PNG)
- Scope overlay image
- Multi-monitor hot-switching
- Alt+1..8 magnification shortcuts
- Full color picker

## License

MIT. Free forever. No strings attached.
