using FluidicML.Gain.Hosting;
using System.Windows;

namespace FluidicML.Gain.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    private readonly SettingsPage _settingsPage;

    public MainWindow(SettingsPage settingsPage)
    {
        _settingsPage = settingsPage;

        Application.Current.MainWindow = this;

        DataContext = this;
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SettingsPageFrame.Navigate(_settingsPage);
    }

    private void MainWindow_TrayLeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
    {
        Show();
    }
}