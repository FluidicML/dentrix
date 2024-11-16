using FluidicML.Gain.Hosting;
using FluidicML.Gain.ViewModels;
using System.Windows;

namespace FluidicML.Gain.Views;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : IWindow
{
    private readonly PipeService _pipeService;
    private readonly DentrixService _dentrixService;
    private readonly SettingsPage _settingsPage;
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly static CancellationTokenSource _cts = new();
    private readonly static CancellationToken _stoppingToken = _cts.Token;

    public MainWindow(
        PipeService pipeService,
        DentrixService dentrixService,
        SettingsPage settingsPage,
        MainWindowViewModel mainWindowViewModel
    )
    {
        _pipeService = pipeService;
        _dentrixService = dentrixService;
        _settingsPage = settingsPage;
        _mainWindowViewModel = mainWindowViewModel;

        Application.Current.MainWindow = this;

        DataContext = this;
        InitializeComponent();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        SettingsPageFrame.Navigate(_settingsPage);

        _ = Task.Run(CheckBackgroundService, _stoppingToken);
        _ = Task.Run(CheckWebSocket, _stoppingToken);
        _ = Task.Run(CheckDentrix, _stoppingToken);
    }

    private async Task CheckBackgroundService()
    {
        while (!_stoppingToken.IsCancellationRequested)
        {
            var status = await _pipeService.QueryBackgroundServiceStatus(_stoppingToken);

            if (status != Status.LOCKED)
            {
                _mainWindowViewModel.StatusBackgroundService = QueryStatusToBool(status);
            }

            await Task.Delay(10_000, _stoppingToken);
        }
    }

    private async Task CheckWebSocket()
    {
        while (!_stoppingToken.IsCancellationRequested)
        {
            var status = await _pipeService.QueryWebSocketStatus(_stoppingToken);

            if (status != Status.LOCKED)
            {
                _mainWindowViewModel.StatusWebSocket = QueryStatusToBool(status);
            }

            await Task.Delay(30_000, _stoppingToken);
        }
    }

    private async Task CheckDentrix()
    {
        while (!_stoppingToken.IsCancellationRequested)
        {
            var status = await _pipeService.QueryDentrixStatus(_stoppingToken);

            if (status != Status.LOCKED)
            {
                _mainWindowViewModel.StatusDentrix = QueryStatusToBool(status);
            }

            if (status == Status.UNHEALTHY)
            {
                var result = await _dentrixService.ConnectAsync(_stoppingToken);

                if (!string.IsNullOrEmpty(result))
                {
                    await _pipeService.SendDentrixConnStr(result, _stoppingToken);
                }
            }

            // TODO: If status is failed, we should try reconnecting to Dentrix.
            await Task.Delay(30_000, _stoppingToken);
        }
    }

    private static bool? QueryStatusToBool(Status status)
    {
        switch (status)
        {
            case Status.HEALTHY:
                {
                    return true;
                }
            case Status.UNHEALTHY:
                {
                    return false;
                }
            default:
                {
                    return null;
                }
        }
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