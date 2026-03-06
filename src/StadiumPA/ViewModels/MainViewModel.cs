using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using StadiumPA.Services;

namespace StadiumPA.ViewModels;

/// <summary>
/// Main view model for Phase 1: master volume, mute toggle, always-on-top toggle.
/// Placeholder properties for Spotify controls, audio buttons, etc. are included
/// so the UI can bind to them (they are non-functional stubs until later phases).
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly MasterVolumeService _masterVolume;
    private readonly SleepSuppressionService _sleepSuppression;

    private float _masterVolumeLevel;
    private bool _isMuted;
    private bool _alwaysOnTop = true;

    // Placeholder values for future phases
    private float _spotifyVolume = 0.80f;

    public MainViewModel()
    {
        _masterVolume = new MasterVolumeService();
        _sleepSuppression = new SleepSuppressionService();

        // Enable sleep suppression on startup
        _sleepSuppression.Enable();

        // Read current system volume
        _masterVolumeLevel = _masterVolume.Volume;
        _isMuted = _masterVolume.IsMuted;

        // Commands
        ToggleMuteCommand = new RelayCommand(ToggleMute);
        ToggleAlwaysOnTopCommand = new RelayCommand(ToggleAlwaysOnTop);

        // Placeholder commands (non-functional stubs for Phase 1)
        SpotifyPrevCommand = new RelayCommand(() => { });
        SpotifyPlayPauseCommand = new RelayCommand(() => { });
        SpotifyNextCommand = new RelayCommand(() => { });
        DimCommand = new RelayCommand(() => { });
        FadeOutCommand = new RelayCommand(() => { });
        KillCommand = new RelayCommand(() => { });
        AnthemCommand = new RelayCommand(() => { });
        GoalCommand = new RelayCommand(() => { });
        TimeoutNextSongCommand = new RelayCommand(() => { });
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

    #region Spotify (Placeholder — Phase 2)

    public float SpotifyVolume
    {
        get => _spotifyVolume;
        set
        {
            if (SetField(ref _spotifyVolume, Math.Clamp(value, 0f, 1f)))
            {
                OnPropertyChanged(nameof(SpotifyVolumePercent));
            }
        }
    }

    public string SpotifyVolumePercent => $"{(int)(_spotifyVolume * 100)}%";

    public ICommand SpotifyPrevCommand { get; }
    public ICommand SpotifyPlayPauseCommand { get; }
    public ICommand SpotifyNextCommand { get; }

    #endregion

    #region Audio Control Buttons (Placeholder — Phase 4)

    public ICommand DimCommand { get; }
    public ICommand FadeOutCommand { get; }
    public ICommand KillCommand { get; }

    #endregion

    #region Local Audio (Placeholder — Phase 3)

    public ICommand AnthemCommand { get; }
    public ICommand GoalCommand { get; }
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
        _masterVolume.Dispose();
    }
}
