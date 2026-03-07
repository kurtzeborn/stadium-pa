# Stadium PA — Usage Guide

## Installation

1. Download the latest zip for your architecture from [GitHub Releases](https://github.com/kurtzeborn/stadium-pa/releases):
   - **StadiumPA-win-x64.zip** — Intel/AMD (most Windows PCs)
   - **StadiumPA-win-arm64.zip** — ARM64 (Surface Pro X, Copilot+ PCs)
2. Extract the zip — you'll get a `StadiumPA` folder containing:
   - `StadiumPA.exe` — the app (single file, no installer needed)
   - `media/` — bundled audio files (goal celebration, national anthem)
3. Run `StadiumPA.exe`. No installation required.

Settings are saved automatically to `%APPDATA%\StadiumPA\settings.json`.

## Pre-Game Setup

When the app launches, a checklist reminds you of these steps:

1. **Open Spotify** — Launch Spotify desktop and start your pregame playlist
2. **Set audio output** — Make sure your PA system is the default Windows audio device
3. **Configure media files** — On first run, use the Settings panel to set file paths for the national anthem and goal celebration audio
4. **Test volume levels** — Use the master and Spotify sliders to verify levels through the PA

## Controls

### Volume Sliders

| Slider | What it controls |
|--------|-----------------|
| **Master Volume** | System-wide volume (Windows audio endpoint) |
| **Spotify Volume** | Spotify's per-process volume only |

### Spotify Transport

| Button | Action |
|--------|--------|
| **⏮ Prev** | Previous track |
| **⏯ Play/Pause** | Toggle Spotify playback |
| **⏭ Next** | Next track |

These send media key events — Spotify doesn't need to be focused.

### Local Audio

| Button | Action |
|--------|--------|
| **National Anthem** | Play/stop the anthem file |
| **Goal Celebration** | Play/stop the goal celebration file |

Both files are pre-loaded into memory on startup for instant playback. A progress bar shows elapsed/total time.

### Audio Control (DIM / FADE OUT / KILL)

These buttons control whatever audio is currently active (Spotify, local audio, or both):

| Button | Behavior | Visual |
|--------|----------|--------|
| **DIM** (toggle) | Fades to dim level (default 10%), press again to restore | Amber border |
| **FADE OUT** (toggle) | Fades to 0% then stops playback, press again to resume | Blue border |
| **KILL** (instant) | Immediately cuts volume to 0% and stops — not a toggle | Red flash |

### Keyboard Hotkeys

Hold **Alt** and press the indicated key. Hotkey badges appear on buttons when Alt is held:

| Hotkey | Action |
|--------|--------|
| **Alt+D** | DIM toggle |
| **Alt+F** | FADE OUT toggle |
| **Alt+K** | KILL |
| **Alt+G** | Goal Celebration |
| **Alt+A** | National Anthem |
| **Alt+Space** | Spotify Play/Pause |

### Settings

Click the **⚙ Settings** button to configure:

- **Anthem file path** — file picker for the national anthem mp3
- **Goal celebration file path** — file picker for the goal celebration mp3
- **Dim level** — volume percentage for DIM mode (default 10%)
- **Fade duration** — how long DIM/FADE OUT transitions take (default 1s)

Settings are persisted to `%APPDATA%\StadiumPA\settings.json` and restored on next launch.

## Game-Day Workflow

### Pregame
1. Launch StadiumPA and Spotify
2. Start your pregame playlist in Spotify
3. Use **Master Volume** and **Spotify Volume** sliders to set levels
4. When ready for the anthem → **DIM** or **FADE OUT** Spotify → press **National Anthem**

### National Anthem
1. Press **National Anthem** — plays the anthem file
2. When finished, anthem stops automatically
3. Resume Spotify (press **FADE OUT** again to undo, or manually play)

### During Play
- Spotify is typically silent during active play
- Use **DIM** for quick PA announcements over music during breaks

### Goal Scored
1. Press **Goal Celebration** — celebration audio plays at full volume
2. Press **DIM** — celebration drops to 10% → announcer calls out the goal scorer
3. Press **DIM** again — celebration returns to full volume
4. Press **FADE OUT** — celebration fades out and stops (play is resuming)

### Timeouts
1. Queue your timeout playlist in Spotify
2. Press **Next** to advance tracks during the timeout
3. **FADE OUT** when timeout ends and play resumes

### Emergency
- Press **KILL** (or **Alt+K**) to instantly silence everything
