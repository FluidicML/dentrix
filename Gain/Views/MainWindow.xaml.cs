using FluidicML.Gain.Hosting;
using System.Windows;

namespace FluidicML.Gain.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    private readonly SettingsPage _settingsPage;
    private readonly DentrixService _dentrixService;

    public MainWindow(SettingsPage settingsPage, DentrixService dentrixService)
    {
        _settingsPage = settingsPage;
        _dentrixService = dentrixService;

        Application.Current.MainWindow = this;

        DataContext = this;
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, System.Windows.RoutedEventArgs e)
    {
        SettingsPageFrame.Navigate(_settingsPage);

        _ = Task.Run(async () =>
        {
            await _dentrixService.ConnectAsync();
        });
    }

    private void MainWindow_TrayLeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
    {
        Show();
    }
}