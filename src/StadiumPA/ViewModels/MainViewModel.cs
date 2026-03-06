using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StadiumPA.Services;

namespace StadiumPA.ViewModels;

/// <summary>
/// Main view model — Phase 1 + Phase 2: master volume, mute, always-on-top,
/// Spotify media key control, Spotify per-process volume, keyboard shortcuts.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MasterVolumeService _masterVolume;
    private readonly SleepSuppressionService _sleepSuppression;
    private readonly SpotifyVolumeService _spotifyVolume;

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

        // Placeholder commands (non-functional stubs until later phases)
        DimCommand = new RelayCommand(() => { });
        FadeOutCommand = new RelayCommand(() => { });
        KillCommand = new RelayCommand(() => { });
        AnthemCommand = new RelayCommand(() => { });
        GoalCommand = new RelayCommand(() => { });
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

    #region Local Audio (Placeholder — Phase 3)

    public ICommand AnthemCommand { get; }
    public ICommand GoalCommand { get; }

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
        _sleepSuppression.Dispose();
        _spotifyVolume.Dispose();
        _masterVolume.Dispose();
    }
}
