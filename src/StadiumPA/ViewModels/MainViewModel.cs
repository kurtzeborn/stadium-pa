using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using System.Windows.Threading;
using StadiumPA.Services;

namespace StadiumPA.ViewModels;

/// <summary>
/// Main view model — Phases 1–3: master volume, mute, always-on-top,
/// Spotify media key control, Spotify per-process volume, keyboard shortcuts,
/// local audio playback (anthem + goal) with elapsed/total time indicators.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MasterVolumeService _masterVolume;
    private readonly SleepSuppressionService _sleepSuppression;
    private readonly SpotifyVolumeService _spotifyVolume;
    private readonly AudioPlayerService _anthemPlayer;
    private readonly AudioPlayerService _goalPlayer;
    private readonly DispatcherTimer _playbackTimer;

    private float _masterVolumeLevel;
    private bool _isMuted;
    private bool _alwaysOnTop = true;

    private float _spotifyVolumeLevel = 0.80f;
    private bool _isSpotifyRunning;

    public MainViewModel()
    {
        _masterVolume = new MasterVolumeService();
        _sleepSuppression = new SleepSuppressionService();
        _spotifyVolume = new SpotifyVolumeService();
        _anthemPlayer = new AudioPlayerService();
        _goalPlayer = new AudioPlayerService();

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

        // Spotify commands
        SpotifyPrevCommand = new RelayCommand(MediaKeyService.PreviousTrack);
        SpotifyPlayPauseCommand = new RelayCommand(MediaKeyService.PlayPause);
        SpotifyNextCommand = new RelayCommand(MediaKeyService.NextTrack);

        // Timeout = next track (same media key)
        TimeoutNextSongCommand = new RelayCommand(MediaKeyService.NextTrack);

        // Local audio commands
        AnthemCommand = new RelayCommand(() => _anthemPlayer.TogglePlayback(), () => _anthemPlayer.IsLoaded);
        GoalCommand = new RelayCommand(() => _goalPlayer.TogglePlayback(), () => _goalPlayer.IsLoaded);
        BrowseAnthemFileCommand = new RelayCommand(BrowseAnthemFile);
        BrowseGoalFileCommand = new RelayCommand(BrowseGoalFile);

        // Placeholder commands (non-functional stubs until later phases)
        DimCommand = new RelayCommand(() => { });
        FadeOutCommand = new RelayCommand(() => { });
        KillCommand = new RelayCommand(() => { });
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

    #region Audio Control Buttons (Placeholder — Phase 4)

    public ICommand DimCommand { get; }
    public ICommand FadeOutCommand { get; }
    public ICommand KillCommand { get; }

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
