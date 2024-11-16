using FluidicML.Gain.Hosting;
using System.Windows;

namespace FluidicML.Gain.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    private readonly PipeService _pipeService;
    private readonly SettingsPage _settingsPage;

    private readonly static CancellationTokenSource _cts = new();
    private readonly static CancellationToken _stoppingToken = _cts.Token;

    public MainWindow(PipeService pipeService, SettingsPage settingsPage)
    {
        _pipeService = pipeService;
        _settingsPage = settingsPage;

        Application.Current.MainWindow = this;

        DataContext = this;
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SettingsPageFrame.Navigate(_settingsPage);

        _ = Task.Run(async () =>
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                await _pipeService.QueryBackgroundServiceStatus(_stoppingToken);
                await Task.Delay(10_000, _stoppingToken);
            }
        }, _stoppingToken);

        _ = Task.Run(async () =>
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                await _pipeService.QueryWebSocketStatus(_stoppingToken);
                await Task.Delay(30_000, _stoppingToken);
            }
        }, _stoppingToken);

        _ = Task.Run(async () =>
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                await _pipeService.QueryDentrixStatus(_stoppingToken);
                await Task.Delay(30_000, _stoppingToken);
            }
        }, _stoppingToken);
    }

    private void MainWindow_TrayLeftClick(Wpf.Ui.Tray.Controls.NotifyIcon sender, RoutedEventArgs e)
    {
        Show();
    }

    private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        _cts.Cancel();
    }
}