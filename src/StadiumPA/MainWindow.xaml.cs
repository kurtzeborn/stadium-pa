using System.Windows;
using System.Windows.Input;
using StadiumPA.ViewModels;

namespace StadiumPA;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        _viewModel = new MainViewModel();
        DataContext = _viewModel;

        // Keyboard shortcuts
        InputBindings.Add(new KeyBinding(_viewModel.SpotifyPlayPauseCommand, Key.Space, ModifierKeys.None));
        InputBindings.Add(new KeyBinding(_viewModel.SpotifyNextCommand, Key.Right, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.SpotifyPrevCommand, Key.Left, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.ToggleMuteCommand, Key.M, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.TimeoutNextSongCommand, Key.T, ModifierKeys.Control));
        InputBindings.Add(new KeyBinding(_viewModel.KillCommand, Key.Escape, ModifierKeys.None));
    }

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        // Refresh Spotify detection when window is focused
        _viewModel.RefreshSpotifyState();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Dispose();
        base.OnClosed(e);
    }
}
