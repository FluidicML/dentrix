using DentrixUI.Hosting;
using System.Windows;
using Wpf.Ui.Tray;

namespace DentrixUI.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    public MainWindow()
    {
        InitializeComponent();

        Application.Current.MainWindow = this;
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