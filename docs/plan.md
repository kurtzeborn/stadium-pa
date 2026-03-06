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
- Simple playback — press to play, press again to stop
- Spotify will typically already be silent when this is used (announcer will have ducked/paused Spotify before asking crowd to stand)
- File path configured in settings (file picker)

### 5. Goal Celebration Button
- Plays a local audio file (`.mp3` or `.wav`) configured in app settings
- Simple playback — press to play, press again to stop
- Spotify will typically already be silent during active play, so no ducking needed
- File path configured in settings (file picker)

### 6. Duck / Announce Button
- A toggle button for when the announcer is about to speak on the PA mic
- On press: smoothly fades Spotify's per-process volume → 0 over ~1 second (Core Audio `ISimpleAudioVolume`)
- On release (or second press): smoothly restores Spotify volume back to its previous level
- Works regardless of which playlist is playing — general-purpose Spotify ducking
- Visual indicator shows ducked state (e.g., button stays highlighted while active)
- This is the primary way music gets turned down before announcements, anthem, etc.

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
- Smooth fade: timer-based interpolation (e.g., 50ms steps over 1 second)
- Immediate mute also available for fast ducking

### Media Key Simulation
```csharp
[DllImport("user32.dll")]
static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

// VK_MEDIA_PLAY_PAUSE = 0xB3
// VK_MEDIA_NEXT_TRACK = 0xB0
// VK_MEDIA_PREV_TRACK = 0xB1
```

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
║  SPOTIFY        ════════════════════●══════           ║
║                                                      ║
║  [⏮ PREV]      [⏯ PLAY/PAUSE]      [⏭ NEXT]       ║
║                                                      ║
║  ┌────────────────────────────────────────────────┐  ║
║  │   🎙 DUCK (ANNOUNCE)                — toggle  │  ║
║  └────────────────────────────────────────────────┘  ║
║                                                      ║
╠══════════════════════════════════════════════════════╣
║                                                      ║
║  ┌────────────────────┐  ┌────────────────────────┐  ║
║  │                    │  │                        │  ║
║  │   🏳 NATIONAL      │  │   🥅 GOAL!             │  ║
║  │     ANTHEM         │  │     CELEBRATION        │  ║
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

- Large, high-contrast buttons for game-day use
- Buttons should be big enough to hit quickly without precise mouse targeting
- Visual feedback: active/playing state highlighted

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
│       ├── Services/
│       │   ├── AudioPlayerService.cs      # NAudio local file playback
│       │   ├── MasterVolumeService.cs     # System volume via Core Audio
│       │   ├── SpotifyVolumeService.cs    # Per-process Spotify volume + ducking
│       │   ├── MediaKeyService.cs         # Simulated media key presses
│       │   └── VolumeFader.cs             # Smooth volume fade utility (duck/restore)
│       ├── ViewModels/
│       │   └── MainViewModel.cs           # MVVM bindings
│       └── Models/
│           └── AppSettings.cs             # Persisted settings (file paths)
├── .gitignore
└── README.md
```

## Settings Persistence

- Store user settings (anthem file path, goal file path, default volumes) in a JSON file
- Location: `%APPDATA%\StadiumPA\settings.json`
- Load on startup, save on change

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
4. **Pregame winding down**: Press Duck → Spotify fades out → make announcements over mic
5. **National Anthem**: Press Anthem button → anthem plays → let it finish
6. 🔄 **Switch Spotify to Timeout playlist**
7. **Pre-game timeout song**: Press Timeout Next Song → plays a song while teams warm up
8. **Game starts**: Pause Spotify; music is off during active play
9. **Goal scored**: Press Goal button → celebration plays (Spotify already silent)
10. **Timeout (1st half)**: Press Timeout Next Song → next song in timeout playlist
11. 🔄 **Halftime starts — switch Spotify to Halftime playlist**, press play
12. **Halftime music**: Spotify plays halftime playlist
13. **Halftime ending**: Press Duck → make announcements → teams return
14. 🔄 **Switch Spotify back to Timeout playlist**
15. **Timeout (2nd half)**: Press Timeout Next Song → continues through timeout playlist
16. **Game ends**: *(Optional)* 🔄 Switch to pregame/halftime playlist, or no music

## Implementation Phases

### Phase 1: Core Skeleton
- [ ] Create WPF project (.NET 8)
- [ ] Main window layout with placeholder buttons
- [ ] Master volume slider (Core Audio)

### Phase 2: Spotify Control
- [ ] Media key simulation (play/pause/next/prev)
- [ ] Spotify per-process volume slider
- [ ] Spotify process detection

### Phase 3: Local Audio Playback
- [ ] NAudio playback engine for local files
- [ ] Anthem button with file picker in settings
- [ ] Goal button with file picker in settings

### Phase 4: Duck / Announce
- [ ] Volume fader utility (smooth interpolation)
- [ ] Duck toggle button — fade Spotify down/up
- [ ] Visual indicator for ducked state

### Phase 5: Polish
- [ ] Large, high-contrast game-day UI
- [ ] Settings persistence (JSON)
- [ ] Error handling (missing files, Spotify not running)
- [ ] Keyboard shortcuts for quick access
