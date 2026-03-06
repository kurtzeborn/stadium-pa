using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using StadiumPA.Services;

namespace StadiumPA.ViewModels;

/// <summary>
/// Main view model — Phases 1–4: master volume, mute, always-on-top,
/// Spotify media key control, Spotify per-process volume, keyboard shortcuts,
/// local audio playback (anthem + goal) with elapsed/total time indicators,
/// DIM/FADE OUT/KILL audio control state machine with smooth volume fading.
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

    private float _masterVolumeLevel;
    private bool _isMuted;
    private bool _alwaysOnTop = true;

    private float _spotifyVolumeLevel = 0.80f;
    private bool _isSpotifyRunning;

    // Audio control state machine (Phase 4)
    private AudioControlState _audioState = AudioControlState.Normal;
    private float _savedSpotifyVol;
    private float _savedAnthemVol = 1.0f;
    private float _savedGoalVol = 1.0f;
    private bool _spotifyWasPausedByUs;
    private bool _anthemWasPausedByUs;
    private bool _goalWasPausedByUs;

    private const float DimLevel = 0.10f;
    private const int FadeDurationMs = 1000;

    public MainViewModel()
    {
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

        // Read current system volume
        _masterVolumeLevel = _masterVolume.Volume;
        _isMuted = _masterVolume.IsMuted;

        // Read Spotify state
        _isSpotifyRunning = _spotifyVolume.IsSpotifyRunning;
        if (_isSpotifyRunning)
        {
            _spotifyVolumeLevel = _spotifyVolume.Volume ?? 0.80f;
        }

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
        set => SetField(ref _alwaysOnTop, value);
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

    private void ExecuteDim()
    {
        _fader.Cancel();

        switch (_audioState)
        {
            case AudioControlState.Normal:
            {
                SaveActiveVolumes();
                var fromS = _spotifyVolume.Volume ?? _savedSpotifyVol;
                var fromA = _anthemPlayer.Volume;
                var fromG = _goalPlayer.Volume;
                SetAudioState(AudioControlState.Dimmed);
                _fader.Start(FadeDurationMs, t =>
                {
                    SetAllActiveVolumes(
                        Lerp(fromS, DimLevel, t),
                        Lerp(fromA, DimLevel, t),
                        Lerp(fromG, DimLevel, t));
                });
                break;
            }

            case AudioControlState.Dimmed:
            {
                var curS = _spotifyVolume.Volume ?? DimLevel;
                var curA = _anthemPlayer.Volume;
                var curG = _goalPlayer.Volume;
                SetAudioState(AudioControlState.Normal);
                _fader.Start(FadeDurationMs, t =>
                {
                    SetAllActiveVolumes(
                        Lerp(curS, _savedSpotifyVol, t),
                        Lerp(curA, _savedAnthemVol, t),
                        Lerp(curG, _savedGoalVol, t));
                });
                break;
            }

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
            {
                SaveActiveVolumes();
                var fromS = _spotifyVolume.Volume ?? _savedSpotifyVol;
                var fromA = _anthemPlayer.Volume;
                var fromG = _goalPlayer.Volume;
                SetAudioState(AudioControlState.FadedOut);
                _fader.Start(FadeDurationMs, t =>
                {
                    SetAllActiveVolumes(
                        Lerp(fromS, 0f, t),
                        Lerp(fromA, 0f, t),
                        Lerp(fromG, 0f, t));
                }, PauseActiveAudio);
                break;
            }

            case AudioControlState.Dimmed:
            {
                var curS = _spotifyVolume.Volume ?? DimLevel;
                var curA = _anthemPlayer.Volume;
                var curG = _goalPlayer.Volume;
                SetAudioState(AudioControlState.FadedOut);
                _fader.Start(FadeDurationMs, t =>
                {
                    SetAllActiveVolumes(
                        Lerp(curS, 0f, t),
                        Lerp(curA, 0f, t),
                        Lerp(curG, 0f, t));
                }, PauseActiveAudio);
                break;
            }

            case AudioControlState.FadedOut:
            {
                ResumeActiveAudio();
                SetAudioState(AudioControlState.Normal);
                _fader.Start(FadeDurationMs, t =>
                {
                    SetAllActiveVolumes(
                        Lerp(0f, _savedSpotifyVol, t),
                        Lerp(0f, _savedAnthemVol, t),
                        Lerp(0f, _savedGoalVol, t));
                });
                break;
            }

            default:
                break; // Killed: no-op
        }
    }

    private void ExecuteKill()
    {
        _fader.Cancel();
        if (_audioState == AudioControlState.Killed) return;

        // Save volumes if coming directly from Normal
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
        _anthemPlayer.LoadFile(path);
        OnPropertyChanged(nameof(AnthemFileDisplay));
        OnPropertyChanged(nameof(AnthemTimeDisplay));
    }

    /// <summary>
    /// Loads a goal celebration audio file for instant playback.
    /// </summary>
    public void LoadGoalFile(string path)
    {
        _goalPlayer.LoadFile(path);
        OnPropertyChanged(nameof(GoalFileDisplay));
        OnPropertyChanged(nameof(GoalTimeDisplay));
    }

    private void BrowseAnthemFile()
    {
        var path = ShowAudioFileDialog("Select National Anthem File");
        if (path is not null) LoadAnthemFile(path);
    }

    private void BrowseGoalFile()
    {
        var path = ShowAudioFileDialog("Select Goal Celebration File");
        if (path is not null) LoadGoalFile(path);
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

/// <summary>
/// State machine for the DIM / FADE OUT / KILL audio controls.
/// </summary>
public enum AudioControlState
{
    Normal,
    Dimmed,
    FadedOut,
    Killed
}
