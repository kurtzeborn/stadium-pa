using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using StadiumPA.Models;
using StadiumPA.Services;

namespace StadiumPA.ViewModels;

/// <summary>
/// Main view model — Phases 1–5: master volume, mute, always-on-top,
/// Spotify media key control, Spotify per-process volume, keyboard shortcuts,
/// local audio playback (anthem + goal) with elapsed/total time indicators,
/// DIM/FADE OUT/KILL audio control state machine with smooth volume fading,
/// settings persistence, error handling, and pre-game checklist.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MasterVolumeService _masterVolume;
    private readonly SleepSuppressionService _sleepSuppression;
    private readonly SpotifyVolumeService _spotifyVolume;
    private readonly AudioPlayerService _anthemPlayer;
    private readonly AudioPlayerService _goalPlayer;
    private readonly DispatcherTimer _playbackTimer;
    private readonly VolumeFader _fader;
    private readonly AppSettings _settings;

    private float _masterVolumeLevel;
    private bool _isMuted;
    private bool _alwaysOnTop = true;

    private float _spotifyVolumeLevel = 0.80f;
    private bool _isSpotifyRunning;

    // Audio control state machine (Phase 4)
    private AudioControlState _audioState = AudioControlState.Normal;
    private float _savedSpotifyVol = 0.80f;
    private float _savedAnthemVol = 1.0f;
    private float _savedGoalVol = 1.0f;
    private bool _spotifyWasPausedByUs;
    private bool _anthemWasPausedByUs;
    private bool _goalWasPausedByUs;

    // Settings-backed values (Phase 5)
    private float _dimLevel;
    private int _fadeDurationMs;
    private string? _statusMessage;

    public MainViewModel()
    {
        // Load persisted settings (returns defaults if no file exists)
        _settings = SettingsService.Load();
        _dimLevel = _settings.DimLevel;
        _fadeDurationMs = _settings.FadeDurationMs;
        _alwaysOnTop = _settings.AlwaysOnTop;

        _masterVolume = new MasterVolumeService();
        _sleepSuppression = new SleepSuppressionService();
        _spotifyVolume = new SpotifyVolumeService();
        _anthemPlayer = new AudioPlayerService();
        _goalPlayer = new AudioPlayerService();
        _fader = new VolumeFader();

        // Timer to update elapsed/total time display during playback
        _playbackTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _playbackTimer.Tick += (_, _) => RefreshPlaybackTimes();

        // Wire playback state changes to start/stop the timer
        _anthemPlayer.PlaybackStateChanged += OnAnyPlaybackStateChanged;
        _goalPlayer.PlaybackStateChanged += OnAnyPlaybackStateChanged;

        // Enable sleep suppression on startup
        _sleepSuppression.Enable();

        // Apply saved default volumes
        _masterVolumeLevel = _settings.DefaultMasterVolume;
        _masterVolume.Volume = _masterVolumeLevel;
        _isMuted = _masterVolume.IsMuted;

        // Apply saved Spotify volume
        _spotifyVolumeLevel = _settings.DefaultSpotifyVolume;
        _isSpotifyRunning = _spotifyVolume.IsSpotifyRunning;
        if (_isSpotifyRunning)
        {
            _spotifyVolume.Volume = _spotifyVolumeLevel;
        }

        // Pre-load audio files from saved paths
        TryLoadAudioFile(_anthemPlayer, _settings.AnthemFilePath, "Anthem");
        TryLoadAudioFile(_goalPlayer, _settings.GoalFilePath, "Goal");

        // Commands
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);

        // Spotify commands (clear killed state on any transport action)
        SpotifyPrevCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); MediaKeyService.PreviousTrack(); });
        SpotifyPlayPauseCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); MediaKeyService.PlayPause(); });
        SpotifyNextCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); MediaKeyService.NextTrack(); });

        // Timeout = next track (same media key)
        TimeoutNextSongCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); MediaKeyService.NextTrack(); });

        // Local audio commands (clear killed state on play)
        AnthemCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); _anthemPlayer.TogglePlayback(); }, () => _anthemPlayer.IsLoaded);
        GoalCommand = new RelayCommand(() => { ClearKilledStateIfNeeded(); _goalPlayer.TogglePlayback(); }, () => _goalPlayer.IsLoaded);
        BrowseAnthemFileCommand = new RelayCommand(BrowseAnthemFile);
        BrowseGoalFileCommand = new RelayCommand(BrowseGoalFile);

        // Audio control commands (Phase 4)
        DimCommand = new RelayCommand(ExecuteDim);
        FadeOutCommand = new RelayCommand(ExecuteFadeOut);
        KillCommand = new RelayCommand(ExecuteKill);
    }

    #region Master Volume

    public float MasterVolumeLevel
    {
        get => _masterVolumeLevel;
        set
        {
            if (SetField(ref _masterVolumeLevel, Math.Clamp(value, 0f, 1f)))
            {
                _masterVolume.Volume = _masterVolumeLevel;
                OnPropertyChanged(nameof(MasterVolumePercent));
            }
        }
    }

    public string MasterVolumePercent => $"{(int)(_masterVolumeLevel * 100)}%";

    public bool IsMuted
    {
        get => _isMuted;
        set
        {
            if (SetField(ref _isMuted, value))
            {
                _masterVolume.IsMuted = _isMuted;
                RefreshChecklist();
            }
        }
    }

    public ICommand ToggleMuteCommand { get; }

    private void ToggleMute() => IsMuted = !IsMuted;

    #endregion

    #region Always On Top

    public bool AlwaysOnTop
    {
        get => _alwaysOnTop;
        set
        {
            if (SetField(ref _alwaysOnTop, value))
                SaveSettings();
        }
    }

    public ICommand ToggleAlwaysOnTopCommand { get; }

    private void ToggleAlwaysOnTop() => AlwaysOnTop = !AlwaysOnTop;

    #endregion

    #region Spotify (Phase 2)

    /// <summary>
    /// Whether Spotify is currently running. Updated on startup; UI can poll or refresh.
    /// </summary>
    public bool IsSpotifyRunning
    {
        get => _isSpotifyRunning;
        private set => SetField(ref _isSpotifyRunning, value);
    }

    public float SpotifyVolume
    {
        get => _spotifyVolumeLevel;
        set
        {
            if (SetField(ref _spotifyVolumeLevel, Math.Clamp(value, 0f, 1f)))
            {
                _spotifyVolume.Volume = _spotifyVolumeLevel;
                OnPropertyChanged(nameof(SpotifyVolumePercent));
            }
        }
    }

    public string SpotifyVolumePercent => $"{(int)(_spotifyVolumeLevel * 100)}%";

    public ICommand SpotifyPrevCommand { get; }
    public ICommand SpotifyPlayPauseCommand { get; }
    public ICommand SpotifyNextCommand { get; }

    /// <summary>
    /// Re-checks whether Spotify is running and syncs the volume slider.
    /// Called from code-behind on window activation or a timer.
    /// </summary>
    public void RefreshSpotifyState()
    {
        IsSpotifyRunning = _spotifyVolume.IsSpotifyRunning;
        if (_isSpotifyRunning)
        {
            var current = _spotifyVolume.Volume;
            if (current.HasValue)
            {
                _spotifyVolumeLevel = current.Value;
                OnPropertyChanged(nameof(SpotifyVolume));
                OnPropertyChanged(nameof(SpotifyVolumePercent));
            }
        }
        RefreshChecklist();
    }

    #endregion

    #region Audio Control Buttons (Phase 4)

    public ICommand DimCommand { get; }
    public ICommand FadeOutCommand { get; }
    public ICommand KillCommand { get; }

    /// <summary>Whether DIM is currently active (amber glow).</summary>
    public bool IsDimActive => _audioState == AudioControlState.Dimmed;

    /// <summary>Whether FADE OUT is currently active (blue glow).</summary>
    public bool IsFadeOutActive => _audioState == AudioControlState.FadedOut;

    /// <summary>Whether KILL was fired and audio hasn't been manually restarted (red glow).</summary>
    public bool IsKilled => _audioState == AudioControlState.Killed;

    private void SetAudioState(AudioControlState state)
    {
        _audioState = state;
        OnPropertyChanged(nameof(IsDimActive));
        OnPropertyChanged(nameof(IsFadeOutActive));
        OnPropertyChanged(nameof(IsKilled));
    }

    private void SaveActiveVolumes()
    {
        _savedSpotifyVol = _spotifyVolumeLevel;
        _savedAnthemVol = _anthemPlayer.Volume;
        _savedGoalVol = _goalPlayer.Volume;
    }

    /// <summary>
    /// Sets volume on all active audio sources directly (bypassing the SpotifyVolume
    /// property setter intentionally — the slider should show the target volume,
    /// not transient fade values).
    /// </summary>
    private void SetAllActiveVolumes(float spotifyVol, float anthemVol, float goalVol)
    {
        _spotifyVolume.Volume = spotifyVol;
        _anthemPlayer.Volume = anthemVol;
        _goalPlayer.Volume = goalVol;
    }

    private void PauseActiveAudio()
    {
        _spotifyWasPausedByUs = _spotifyVolume.IsSpotifyActive;
        if (_spotifyWasPausedByUs)
            MediaKeyService.PlayPause();

        _anthemWasPausedByUs = _anthemPlayer.IsPlaying;
        if (_anthemWasPausedByUs)
            _anthemPlayer.Pause();

        _goalWasPausedByUs = _goalPlayer.IsPlaying;
        if (_goalWasPausedByUs)
            _goalPlayer.Pause();
    }

    private void ResumeActiveAudio()
    {
        if (_spotifyWasPausedByUs)
            MediaKeyService.PlayPause();
        if (_anthemWasPausedByUs)
            _anthemPlayer.Resume();
        if (_goalWasPausedByUs)
            _goalPlayer.Resume();

        _spotifyWasPausedByUs = false;
        _anthemWasPausedByUs = false;
        _goalWasPausedByUs = false;
    }

    /// <summary>
    /// If audio was killed, restores saved volumes and clears killed state.
    /// Called before any manual play command so the user can restart normally.
    /// </summary>
    private void ClearKilledStateIfNeeded()
    {
        if (_audioState != AudioControlState.Killed) return;

        _spotifyVolume.Volume = _savedSpotifyVol;
        _anthemPlayer.Volume = _savedAnthemVol;
        _goalPlayer.Volume = _savedGoalVol;
        SetAudioState(AudioControlState.Normal);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Captures current volume levels from all sources and fades to target.
    /// </summary>
    private void FadeToLevel(float targetLevel, Action? onComplete = null)
    {
        var fromS = _spotifyVolume.Volume ?? _savedSpotifyVol;
        var fromA = _anthemPlayer.Volume;
        var fromG = _goalPlayer.Volume;
        _fader.Start(_fadeDurationMs, t =>
        {
            SetAllActiveVolumes(
                Lerp(fromS, targetLevel, t),
                Lerp(fromA, targetLevel, t),
                Lerp(fromG, targetLevel, t));
        }, onComplete);
    }

    /// <summary>
    /// Fades from current levels back to the previously saved volumes.
    /// </summary>
    private void FadeToSavedLevels()
    {
        var fromS = _spotifyVolume.Volume ?? 0f;
        var fromA = _anthemPlayer.Volume;
        var fromG = _goalPlayer.Volume;
        _fader.Start(_fadeDurationMs, t =>
        {
            SetAllActiveVolumes(
                Lerp(fromS, _savedSpotifyVol, t),
                Lerp(fromA, _savedAnthemVol, t),
                Lerp(fromG, _savedGoalVol, t));
        });
    }

    private void ExecuteDim()
    {
        _fader.Cancel();

        switch (_audioState)
        {
            case AudioControlState.Normal:
                SaveActiveVolumes();
                SetAudioState(AudioControlState.Dimmed);
                FadeToLevel(_dimLevel);
                break;

            case AudioControlState.Dimmed:
                SetAudioState(AudioControlState.Normal);
                FadeToSavedLevels();
                break;

            default:
                break; // FadedOut, Killed: no-op
        }
    }

    private void ExecuteFadeOut()
    {
        _fader.Cancel();

        switch (_audioState)
        {
            case AudioControlState.Normal:
                SaveActiveVolumes();
                SetAudioState(AudioControlState.FadedOut);
                FadeToLevel(0f, PauseActiveAudio);
                break;

            case AudioControlState.Dimmed:
                SetAudioState(AudioControlState.FadedOut);
                FadeToLevel(0f, PauseActiveAudio);
                break;

            case AudioControlState.FadedOut:
                ResumeActiveAudio();
                SetAudioState(AudioControlState.Normal);
                FadeToSavedLevels();
                break;

            default:
                break; // Killed: no-op
        }
    }

    private void ExecuteKill()
    {
        _fader.Cancel();
        if (_audioState == AudioControlState.Killed) return;

        // Dimmed/FadedOut states already saved volumes on entry;
        // only need to capture if coming directly from Normal.
        if (_audioState == AudioControlState.Normal)
            SaveActiveVolumes();

        // Instant volume cut
        SetAllActiveVolumes(0f, 0f, 0f);

        // Stop all local audio playback
        if (_anthemPlayer.IsPlaying || _anthemPlayer.IsPaused) _anthemPlayer.Stop();
        if (_goalPlayer.IsPlaying || _goalPlayer.IsPaused) _goalPlayer.Stop();

        // Pause Spotify only if it's actively producing audio
        if (_spotifyVolume.IsSpotifyActive)
            MediaKeyService.PlayPause();

        SetAudioState(AudioControlState.Killed);
    }

    #endregion

    #region Local Audio (Phase 3)

    public ICommand AnthemCommand { get; }
    public ICommand GoalCommand { get; }
    public ICommand BrowseAnthemFileCommand { get; }
    public ICommand BrowseGoalFileCommand { get; }

    /// <summary>Anthem file path display text for the UI.</summary>
    public string AnthemFileDisplay => _anthemPlayer.IsLoaded
        ? System.IO.Path.GetFileName(_anthemPlayer.FilePath!)
        : "(not configured)";

    /// <summary>Goal file path display text for the UI.</summary>
    public string GoalFileDisplay => _goalPlayer.IsLoaded
        ? System.IO.Path.GetFileName(_goalPlayer.FilePath!)
        : "(not configured)";

    /// <summary>Whether the anthem is currently playing (for green glow state).</summary>
    public bool IsAnthemPlaying => _anthemPlayer.IsPlaying;

    /// <summary>Whether the goal celebration is currently playing (for green glow state).</summary>
    public bool IsGoalPlaying => _goalPlayer.IsPlaying;

    /// <summary>Formatted elapsed / total for anthem: "1:23 / 2:10"</summary>
    public string AnthemTimeDisplay => FormatTimeDisplay(_anthemPlayer);

    /// <summary>Formatted elapsed / total for goal: "0:45 / 1:02"</summary>
    public string GoalTimeDisplay => FormatTimeDisplay(_goalPlayer);

    /// <summary>
    /// Loads an anthem audio file for instant playback.
    /// </summary>
    public void LoadAnthemFile(string path)
    {
        try
        {
            _anthemPlayer.LoadFile(path);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Anthem load error: {ex.Message}";
        }
        OnPropertyChanged(nameof(AnthemFileDisplay));
        OnPropertyChanged(nameof(AnthemTimeDisplay));
    }

    /// <summary>
    /// Loads a goal celebration audio file for instant playback.
    /// </summary>
    public void LoadGoalFile(string path)
    {
        try
        {
            _goalPlayer.LoadFile(path);
            StatusMessage = null;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Goal load error: {ex.Message}";
        }
        OnPropertyChanged(nameof(GoalFileDisplay));
        OnPropertyChanged(nameof(GoalTimeDisplay));
    }

    private void BrowseAnthemFile()
    {
        var path = ShowAudioFileDialog("Select National Anthem File");
        if (path is null) return;
        LoadAnthemFile(path);
        SaveSettings();
        RefreshChecklist();
    }

    private void BrowseGoalFile()
    {
        var path = ShowAudioFileDialog("Select Goal Celebration File");
        if (path is null) return;
        LoadGoalFile(path);
        SaveSettings();
        RefreshChecklist();
    }

    private static string? ShowAudioFileDialog(string title)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = title,
            Filter = "Audio Files|*.mp3;*.wav|All Files|*.*",
            CheckFileExists = true,
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    private static string FormatTimeDisplay(AudioPlayerService player)
    {
        if (!player.IsLoaded) return "—:— / —:—";

        var elapsed = player.CurrentTime;
        var total = player.TotalTime;
        return $"{(int)elapsed.TotalMinutes}:{elapsed.Seconds:D2} / {(int)total.TotalMinutes}:{total.Seconds:D2}";
    }

    private void OnAnyPlaybackStateChanged()
    {
        // Start or stop the UI timer based on whether anything is playing
        if (_anthemPlayer.IsPlaying || _goalPlayer.IsPlaying)
        {
            if (!_playbackTimer.IsEnabled) _playbackTimer.Start();
        }
        else
        {
            _playbackTimer.Stop();
        }

        RefreshPlaybackTimes();
        OnPropertyChanged(nameof(IsAnthemPlaying));
        OnPropertyChanged(nameof(IsGoalPlaying));
    }

    private void RefreshPlaybackTimes()
    {
        OnPropertyChanged(nameof(AnthemTimeDisplay));
        OnPropertyChanged(nameof(GoalTimeDisplay));
    }

    #endregion

    #region Timeout (Phase 2 — uses media key next track)

    public ICommand TimeoutNextSongCommand { get; }

    #endregion

    #region Settings & Status (Phase 5)

    /// <summary>Dim level (0.0–1.0). Persisted to settings.</summary>
    public float DimLevel
    {
        get => _dimLevel;
        set
        {
            if (SetField(ref _dimLevel, Math.Clamp(value, 0.01f, 1f)))
            {
                OnPropertyChanged(nameof(DimLevelPercent));
                OnPropertyChanged(nameof(DimButtonSubtext));
                SaveSettings();
            }
        }
    }

    public string DimLevelPercent => $"{(int)(_dimLevel * 100)}%";

    public string DimButtonSubtext => $"\u2193{DimLevelPercent} (toggle)";

    /// <summary>Fade duration in seconds (0.2–3.0). Persisted as milliseconds.</summary>
    public float FadeDurationSeconds
    {
        get => _fadeDurationMs / 1000f;
        set
        {
            var ms = (int)(Math.Clamp(value, 0.2f, 3.0f) * 1000);
            if (_fadeDurationMs != ms)
            {
                _fadeDurationMs = ms;
                OnPropertyChanged(nameof(FadeDurationSeconds));
                OnPropertyChanged(nameof(FadeDurationDisplay));
                SaveSettings();
            }
        }
    }

    public string FadeDurationDisplay => $"{_fadeDurationMs / 1000.0:F1}s";

    /// <summary>Status/error message shown in the UI. Null when no message.</summary>
    public string? StatusMessage
    {
        get => _statusMessage;
        private set => SetField(ref _statusMessage, value);
    }

    /// <summary>
    /// Loads an audio file on startup with error handling. Does not save settings.
    /// </summary>
    private void TryLoadAudioFile(AudioPlayerService player, string? path, string label)
    {
        if (string.IsNullOrWhiteSpace(path)) return;

        try
        {
            player.LoadFile(path);
            if (!player.IsLoaded)
                StatusMessage = $"{label} file not found: {System.IO.Path.GetFileName(path)}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"{label} load failed: {ex.Message}";
        }
    }

    /// <summary>
    /// Snapshots current state to %APPDATA%\StadiumPA\settings.json.
    /// </summary>
    private void SaveSettings()
    {
        _settings.AnthemFilePath = _anthemPlayer.FilePath;
        _settings.GoalFilePath = _goalPlayer.FilePath;
        _settings.FadeDurationMs = _fadeDurationMs;
        _settings.DimLevel = _dimLevel;
        _settings.DefaultMasterVolume = _masterVolumeLevel;
        _settings.DefaultSpotifyVolume = _spotifyVolumeLevel;
        _settings.AlwaysOnTop = _alwaysOnTop;
        SettingsService.Save(_settings);
    }

    #endregion

    #region Pre-Game Checklist (Phase 5)

    public bool IsAnthemReady => _anthemPlayer.IsLoaded;
    public bool IsGoalReady => _goalPlayer.IsLoaded;
    public bool IsSpotifyReady => _isSpotifyRunning;
    public bool IsVolumeReady => _masterVolumeLevel > 0.01f && !_isMuted;

    public string AnthemStatusText => _anthemPlayer.IsLoaded
        ? $"\u2705 Anthem: {System.IO.Path.GetFileName(_anthemPlayer.FilePath!)}"
        : "\u274c Anthem not configured";

    public string GoalStatusText => _goalPlayer.IsLoaded
        ? $"\u2705 Goal: {System.IO.Path.GetFileName(_goalPlayer.FilePath!)}"
        : "\u274c Goal not configured";

    public string SpotifyStatusText => _isSpotifyRunning
        ? "\u2705 Spotify detected"
        : "\u26a0 Spotify not detected";

    public string VolumeStatusText => IsVolumeReady
        ? $"\u2705 Volume: {MasterVolumePercent}"
        : _isMuted ? "\u26a0 System audio is muted" : "\u26a0 Master volume is at 0%";

    private void RefreshChecklist()
    {
        OnPropertyChanged(nameof(IsAnthemReady));
        OnPropertyChanged(nameof(IsGoalReady));
        OnPropertyChanged(nameof(IsSpotifyReady));
        OnPropertyChanged(nameof(IsVolumeReady));
        OnPropertyChanged(nameof(AnthemStatusText));
        OnPropertyChanged(nameof(GoalStatusText));
        OnPropertyChanged(nameof(SpotifyStatusText));
        OnPropertyChanged(nameof(VolumeStatusText));
    }

    #endregion

    #region INotifyPropertyChanged

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(name);
        return true;
    }

    #endregion

    public void Dispose()
    {
        SaveSettings();
        _fader.Dispose();
        _playbackTimer.Stop();
        _anthemPlayer.PlaybackStateChanged -= OnAnyPlaybackStateChanged;
        _goalPlayer.PlaybackStateChanged -= OnAnyPlaybackStateChanged;
        _anthemPlayer.Dispose();
        _goalPlayer.Dispose();
        _sleepSuppression.Dispose();
        _spotifyVolume.Dispose();
        _masterVolume.Dispose();
    }
}
