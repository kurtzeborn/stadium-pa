using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Input;
using System.Windows.Navigation;
using StadiumPA.ViewModels;

namespace StadiumPA;

public partial class AboutDialog : Window
{
    public AboutDialog(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "unknown";
        VersionText.Text = $"v{version}";
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        e.Handled = true;
    }

    private void CloseWindow_Executed(object sender, ExecutedRoutedEventArgs e) => Close();
}
