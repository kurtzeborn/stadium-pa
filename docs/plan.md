# Stadium PA — App Plan

## Overview

A Windows desktop app (C# / WPF) for managing audio during lacrosse games. The app controls Spotify playback via media key simulation, plays local audio files (national anthem, goal celebration), provides a "duck" button for fading Spotify when making PA announcements, and provides master system volume control — all fully offline.

## Architecture

**Framework**: C# / WPF (.NET 8)
**Target OS**: Windows 10/11 only
**Audio**: NAudio + Windows Core Audio API
**Spotify Control**: Media key simulation (no internet required)
**Local Playback**: NAudio `WaveOutEvent` / `AudioFileReader`

```
┌─────────────────────────────────────────────┐
│              WPF UI (MainWindow)             │
│                                              │
│  ┌──────────┐ ┌──────────┐ ┌──────────────┐ │
│  │ Master   │ │ Spotify  │ │ Local Audio  │ │
│  │ Volume   │ │ Controls │ │ (Anthem/Goal)│ │
│  └────┬─────┘ └────┬─────┘ └──────┬───────┘ │
│       │             │              │         │
├───────┼─────────────┼──────────────┼─────────┤
│       ▼             ▼              ▼         │
│  Core Audio    Media Key      NAudio         │
│  API (MMDev)   Simulation     Playback       │
│       │             │              │         │
│       ▼             ▼              ▼         │
│  System Vol    Spotify.exe    App Audio       │
│  Endpoint      (offline)      Stream          │
│       │             │              │         │
│       └─────────────┴──────────────┘         │
│                     │                        │
│          Windows Audio Mixer                 │
└─────────────────────────────────────────────┘
```

## Features

### 1. Master Volume Control
- Slider controlling system-wide volume via `MMDeviceEnumerator` / `AudioEndpointVolume`
- Uses Windows Core Audio API (`NAudio.CoreAudioApi`)
- Mute toggle button

### 2. Spotify Playback Control (Offline via Media Keys)
- **Play/Pause** button — sends `VK_MEDIA_PLAY_PAUSE` via `SendInput`
- **Next Track** button — sends `VK_MEDIA_NEXT_TRACK`
- **Previous Track** button — sends `VK_MEDIA_PREV_TRACK`
- Spotify volume slider — controls Spotify's per-process audio volume via Core Audio `ISimpleAudioVolume` (not the Spotify API)
- User manually selects playlists in Spotify; app handles transport + volume

### 3. Timeout Playlist — "Next Song" Button
- Sends `VK_MEDIA_NEXT_TRACK` media key to advance Spotify to next track
- Since this uses the same Spotify transport, the user should have the timeout playlist queued in Spotify when timeouts begin
- Alternative: make a dedicated "Timeout" playlist in Spotify, and the user switches to it once; then "Next Song" just sends next-track

### 4. National Anthem Button
- Plays a local audio file (`.mp3` or `.wav`) configured in app settings
- **Pre-loaded into memory on startup** — no delay when pressed
- Simple playback — press to play, press again to stop
- Elapsed / total time progress indicator shown during playback
- Spotify will typically already be silent when this is used (announcer will have ducked/paused Spotify before asking crowd to stand)
- File path configured in settings (file picker)

### 5. Goal Celebration Button
- Plays a local audio file (`.mp3` or `.wav`) configured in app settings
- **Pre-loaded into memory on startup** — no delay when pressed
- Elapsed / total time progress indicator shown during playback
- No looping — file plays once and ends naturally (audio file fades out on its own)
- Spotify will typically already be silent during active play
- Multi-step flow during a goal:
  1. **Press Goal** → celebration plays at 100% volume
  2. **Press DIM** → celebration lowers to 10% → announcer calls out who scored
  3. **Press DIM again** → celebration returns to 100%
  4. **Press FADE OUT** → celebration fades to 0 and stops (play is resuming)
- File path configured in settings (file picker)

### 6. Audio Control Buttons

Three buttons for controlling whatever audio is currently playing (Spotify or local files).
All three act on the **active audio source** — whichever of Spotify or local audio is currently producing sound. If both are somehow playing, they act on both.

#### 6a. DIM (toggle)
- **First press**: smoothly fades active audio → **dim level** (default 10%) over **fade duration** (default 1s)
- **Second press**: smoothly restores back to previous volume level over same fade duration
- Use case: talking over music (e.g., announcing a goal scorer while celebration plays, or making a quick comment over pregame music)
- **Color-coded state**: button glows **amber/yellow** border while dimmed
- Audio continues playing — just quieter

#### 6b. FADE OUT (toggle)
- **First press**: smoothly fades active audio → **0%** over **fade duration** (default 1s), then **pauses/stops** playback
- **Second press**: **resumes** playback and smoothly restores volume
- Use case: ending a segment cleanly (e.g., pregame winding down, stopping goal celebration when play resumes)
- **Color-coded state**: button glows **blue** border while faded out

#### 6c. KILL (instant)
- **Press**: immediately sets volume to **0%** and **pauses/stops** playback — no fade
- Not a toggle — one-shot emergency cut
- Use case: something unexpected happens, wrong song playing, need instant silence
- Playback must be manually restarted after a kill

### 7. Always on Top
- App window stays above all other windows (Spotify, browser, etc.)
- Enabled by default — toggle in title bar or settings
- Uses `Topmost = true` in WPF

### 8. Sleep / Screen Suppression
- App prevents Windows from sleeping the display or the machine while running
- Uses `SetThreadExecutionState` P/Invoke (`ES_CONTINUOUS | ES_SYSTEM_REQUIRED | ES_DISPLAY_REQUIRED`)
- Automatically restored when app closes

## Key Technical Components

### Core Audio Per-Process Volume (Spotify Ducking)
```
MMDeviceEnumerator
  → GetDefaultAudioEndpoint(DataFlow.Render)
    → AudioSessionManager2
      → Enumerate sessions
        → Find session where Process.ProcessName == "Spotify"
          → SimpleAudioVolume.Volume = 0.0f .. 1.0f
```
- Smooth fade: timer-based interpolation (e.g., 50ms steps over configurable duration)
- Fade duration and dim level are user-configurable in settings (see Settings Persistence)
- Three audio control modes:
  - **DIM**: fade → configurable dim level (default 10%), keep playing (toggle)
  - **FADE OUT**: fade → 0%, then pause/stop (toggle)
  - **KILL**: instant → 0% + stop (one-shot)
- Fader needs to be reusable for both Spotify per-process volume and NAudio playback volume
- Must detect active audio source: Spotify (via Core Audio session), local audio (via NAudio playback state)

### Audio File Pre-loading
- Anthem and Goal celebration files are read into memory (`MemoryStream`) on app startup
- Avoids 200-500ms NAudio initialization delay at playback time
- Files are re-loaded if the path changes in settings

### DIM / FADE OUT / KILL State Machine
```
                   ┌──────────────┐
          ┌───────►│   NORMAL     │◄────────────────────┐
          │        │  (vol 100%)  │                     │
          │        └──┬───┬───┬──┘                     │
          │    DIM    │   │   │  KILL                   │
          │           ▼   │   ▼                         │
          │   ┌───────────┐   ┌──────────┐              │
   DIM    │   │  DIMMED   │   │  KILLED  │  (manual     │
  (undo)  │   │ (vol 10%) │   │ (vol 0%, │   restart    │
          │   │ playing   │   │  stopped)│   required)  │
          │   └──┬────┬───┘   └──────────┘              │
          │      │    │                                 │
          └──────┘    │ FADE OUT                        │
                      ▼                                 │
              ┌──────────────┐                          │
              │  FADED OUT   │──── FADE OUT (undo) ─────┘
              │  (vol 0%,    │
              │   stopped)   │
              └──────────────┘
```
**Key transitions:**
- NORMAL → DIM → NORMAL (toggle)
- NORMAL → FADE OUT → NORMAL (toggle)
- DIMMED → FADE OUT → fades from 10% → 0% + stops (does not restore first)
- DIMMED → KILL → instant 0% + stop (clears DIM state)
- FADED OUT → DIM → no-op (audio is stopped, nothing to dim)
- FADED OUT → KILL → no-op (already silent)
- KILLED → any button except manual restart → no-op

### Media Key Simulation
```csharp
[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

// VK_MEDIA_PLAY_PAUSE = 0xB3
// VK_MEDIA_NEXT_TRACK = 0xB0
// VK_MEDIA_PREV_TRACK = 0xB1
```
> **⚠ Media Key Conflict**: Media keys are broadcast system-wide. If a browser (YouTube, Twitch) or other media app is open, play/pause/next will affect all of them. Close all other media-capable apps before the game.

### Local Audio Playback (NAudio)
```csharp
var reader = new AudioFileReader("anthem.mp3");
var waveOut = new WaveOutEvent();
waveOut.Init(reader);
waveOut.Play();
```

## UI Layout (Conceptual)

```
╔══════════════════════════════════════════════════════╗
║  STADIUM PA                                          ║
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  MASTER VOLUME  ════════════════════════●══  [MUTE]  ║
║                                                      ║
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  SPOTIFY        ════════════════════●══════          ║
║                                                      ║
║  [⏮ PREV]      [⏯ PLAY/PAUSE]      [⏭ NEXT]         ║
║                                                      ║
╠══════════════════════════════════════════════════════╣
║  AUDIO CONTROLS                                      ║
║                                                      ║
║  ┌─────────────┐ ┌───────────────┐ ┌───────────────┐ ║
║  │  🔉 DIM     │ │  🔇 FADE OUT │ │  ⚠ KILL       │ ║
║  │  ↓10%       │ │  ↓0% + stop   │ │  instant off  │ ║
║  │  (toggle)   │ │  (toggle)     │ │  (one-shot)   │ ║
║  └─────────────┘ └───────────────┘ └───────────────┘ ║
║                                                      ║
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  ┌────────────────────┐  ┌────────────────────────┐  ║
║  │                    │  │                        │  ║
║  │   🏳 NATIONAL      │  │   🥅 GOAL!             │  ║
║  │     ANTHEM         │  │     CELEBRATION        │  ║
║  │   1:23 / 2:10      │  │   0:45 / 1:02          │  ║
║  │                    │  │                        │  ║
║  └────────────────────┘  └────────────────────────┘  ║
║                                                      ║
║  ┌────────────────────────────────────────────────┐  ║
║  │   ⏱ TIMEOUT — NEXT SONG                       │  ║
║  └────────────────────────────────────────────────┘  ║
║                                                      ║
╠══════════════════════════════════════════════════════╣
║  Settings: [Anthem File: C:\music\anthem.mp3    📂]  ║
║            [Goal File:   C:\music\goal.mp3      📂]  ║
╚══════════════════════════════════════════════════════╝
```

- **Dark theme** — high-contrast dark background for readability in sun or shade
- **Touch-first design** — primary input is a touch screen laptop; all buttons must be large enough to avoid fat-fingering (minimum ~80x80dp hit targets, generous spacing between buttons)
- Keyboard shortcuts available as a secondary input method
- **Color-coded button states** — glowing borders so the operator can track state at a glance:
  - **Amber/yellow glow** = DIM active (audio at dim level, still playing)
  - **Blue glow** = FADE OUT active (audio stopped, ready to restore)
  - **Red glow** = KILL fired (audio killed, manual restart needed)
  - **Green glow** = Anthem or Goal currently playing
  - **No glow** = idle / normal
- Always-on-top by default

## NuGet Dependencies

| Package | Purpose |
|---|---|
| `NAudio` | Local file playback + Core Audio API access |
| (built-in) | `user32.dll` P/Invoke for media key simulation |
| (built-in) | WPF for UI |

## Project Structure

```
stadium-pa/
├── docs/
│   └── plan.md
├── src/
│   └── StadiumPA/
│       ├── StadiumPA.csproj
│       ├── App.xaml
│       ├── App.xaml.cs
│       ├── MainWindow.xaml
│       ├── MainWindow.xaml.cs
│       ├── Converters/
│       │   └── BoolToMuteConverter.cs     # Bool → mute/unmute icon
│       ├── Services/
│       │   ├── AudioPlayerService.cs      # NAudio local file playback (Phase 3)
│       │   ├── MasterVolumeService.cs     # System volume via Core Audio
│       │   ├── MediaKeyService.cs         # Simulated media key presses (Phase 2)
│       │   ├── SleepSuppressionService.cs # Prevent sleep/screen off
│       │   ├── SpotifyVolumeService.cs    # Per-process Spotify volume (Phase 2)
│       │   └── VolumeFader.cs             # Smooth volume fade utility (Phase 4)
│       ├── ViewModels/
│       │   ├── MainViewModel.cs           # MVVM bindings
│       │   └── RelayCommand.cs            # ICommand implementation
│       └── Models/
│           └── AppSettings.cs             # Persisted settings (Phase 5)
├── .gitignore
└── README.md
```

## Settings Persistence

- Store user settings in a JSON file
- Location: `%APPDATA%\StadiumPA\settings.json`
- Load on startup, save on change

**Persisted settings:**
| Setting | Default | Description |
|---|---|---|
| `anthemFilePath` | *(none)* | Path to national anthem audio file |
| `goalFilePath` | *(none)* | Path to goal celebration audio file |
| `fadeDurationMs` | `1000` | Duration of DIM / FADE OUT fades in milliseconds |
| `dimLevel` | `0.10` | Volume level for DIM (0.0–1.0) |
| `defaultMasterVolume` | `0.80` | Master volume on startup |
| `defaultSpotifyVolume` | `0.80` | Spotify volume on startup |
| `alwaysOnTop` | `true` | Keep window above all others |

## Pre-Game Setup Checklist

The app should display or the operator should verify before each game:

- [ ] Correct audio output device selected (headphone jack → PA system)
- [ ] Windows power/sleep set to "Never" (or app is suppressing it)
- [ ] Spotify open with playlists downloaded for offline use
- [ ] Pregame playlist selected and ready
- [ ] Anthem and Goal celebration files loaded (verify with a quick test play)
- [ ] Close all other media-capable apps (browsers, media players) to avoid media key conflicts
- [ ] Master volume and Spotify volume at desired starting levels

## Spotify Playlists

Three curated playlists, all downloaded for offline use:

| Playlist | When Used |
|---|---|
| **Pregame** | Background music before the game |
| **Timeout** | One song per timeout / stoppage during play |
| **Halftime** | Background music during halftime |

**Postgame**: No dedicated playlist — reuse pregame or halftime, or no music.

### Manual Playlist Switches (in Spotify)

There are **4 manual playlist switches** during a game. Each is a quick tap in the Spotify UI during a natural break:

1. **Before arriving**: Select **Pregame** playlist in Spotify
2. **Pregame ending → Game start**: Switch to **Timeout** playlist
3. **Halftime starts**: Switch to **Halftime** playlist
4. **Second half starts**: Switch back to **Timeout** playlist
5. *(Optional)* **Postgame**: Switch to **Pregame** or **Halftime** playlist if desired

## Game Day Workflow

1. **Pre-game setup**: Open Spotify, select **Pregame** playlist, press play
2. **Launch Stadium PA**: Master volume and Spotify volume sliders ready
3. **Pregame music**: Spotify plays pregame playlist; adjust volume with sliders
4. **Pregame winding down**: Press FADE OUT → Spotify fades to silence + pauses → make announcements over mic
5. **National Anthem**: Press Anthem button → anthem plays → let it finish
6. 🔄 **Switch Spotify to Timeout playlist**
7. **Pre-game timeout song**: Press Timeout Next Song → plays a song while teams warm up
8. **Game starts**: Press FADE OUT (or KILL) → music stops; silence during active play
9. **Goal scored**: Press Goal → celebration at 100% → DIM to 10% → announce scorer → DIM again to 100% → FADE OUT to stop when play resumes
10. **Timeout (1st half)**: Press Timeout Next Song → next song in timeout playlist
11. 🔄 **Halftime starts — switch Spotify to Halftime playlist**, press play
12. **Halftime music**: Spotify plays halftime playlist
13. **Halftime ending**: Press FADE OUT → Spotify fades + pauses → make announcements
14. 🔄 **Switch Spotify back to Timeout playlist**
15. **Timeout (2nd half)**: Press Timeout Next Song → continues through timeout playlist
16. **Game ends**: *(Optional)* 🔄 Switch to pregame/halftime playlist, or no music

## Implementation Phases

### Phase 1: Core Skeleton ✅
- [x] Create WPF project (.NET 8)
- [x] Dark theme, touch-friendly layout with placeholder buttons
- [x] Master volume slider (Core Audio)
- [x] Always-on-top window (default on, toggle)
- [x] Sleep/screen suppression (`SetThreadExecutionState`)

### Phase 2: Spotify Control
- [ ] Media key simulation (play/pause/next/prev)
- [ ] Spotify per-process volume slider
- [ ] Spotify process detection
- [ ] Keyboard shortcuts for core controls (KILL as global hotkey)

### Phase 3: Local Audio Playback
- [ ] NAudio playback engine for local files
- [ ] Audio file pre-loading into memory on startup
- [ ] Anthem button with file picker in settings
- [ ] Goal button with file picker in settings
- [ ] Elapsed / total time progress indicator on Anthem and Goal

### Phase 4: Audio Control Buttons (DIM / FADE OUT / KILL)
- [ ] Volume fader utility (smooth interpolation, configurable target level)
- [ ] Active audio source detection (Spotify session, local playback, or both)
- [ ] DIM toggle — fade to 10%, restore to previous
- [ ] FADE OUT toggle — fade to 0% + pause/stop, restore + resume
- [ ] KILL one-shot — instant 0% + stop
- [ ] Color-coded button state visuals (amber=DIM, blue=FADE OUT, red=KILL, green=playing)
- [ ] State machine: DIM → FADE OUT transitions correctly, KILL clears all states

### Phase 5: Polish
- [ ] Settings persistence (JSON)
- [ ] Error handling (missing files, Spotify not running)
- [ ] Pre-game checklist screen or verification
- [ ] Final touch/UX pass — button sizing, spacing, visual feedback
