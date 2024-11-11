using DentrixUI.Hosting;
using System.Windows;

namespace DentrixUI.Views;

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

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;

        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;
        WindowState = WindowState.Minimized;
        Hide();  // Can show this again from the tray icon.
    }

    private void MainWindow_TrayLeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
    {
        Show();
    }
}