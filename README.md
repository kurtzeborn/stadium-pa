# Stadium PA

Windows desktop app for managing audio during lacrosse games. Controls Spotify playback, plays local audio files (national anthem, goal celebration), and provides DIM/FADE OUT/KILL controls for smooth audio transitions during PA announcements.

## Features

- **Spotify Control** — Play/pause, next/prev, per-process volume slider (no internet required)
- **Local Audio** — National anthem and goal celebration with instant playback (pre-loaded)
- **DIM / FADE OUT / KILL** — Smooth audio transitions for PA announcements
- **Master Volume** — System-wide volume control
- **Always on Top** — Stays visible over Spotify and other apps
- **Sleep Suppression** — Prevents display/system sleep during games
- **Keyboard Hotkeys** — Alt+key shortcuts for hands-free operation

## Tech Stack

- C# / WPF (.NET 8)
- NAudio 2.2.1 + Windows Core Audio API
- Windows 10/11 only

## Getting Started

Download the latest release from [GitHub Releases](https://github.com/kurtzeborn/stadium-pa/releases), or build from source:

```powershell
dotnet build src/StadiumPA/StadiumPA.csproj
dotnet run --project src/StadiumPA
```

See the [Usage Guide](docs/USAGE.md) for setup and operation instructions.

## Releasing

```powershell
.\release.ps1
```

Tags and pushes to trigger the [GitHub Actions release workflow](.github/workflows/release.yml), which builds self-contained single-file EXEs for both `win-x64` and `win-arm64`.

## Docs

- [Usage Guide](docs/USAGE.md) — Setup, controls, and game-day workflow
- [App Plan](docs/plan.md) — Architecture and feature design
