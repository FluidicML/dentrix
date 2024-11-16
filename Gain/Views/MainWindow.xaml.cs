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
    private readonly SettingsPage _settingsPage;
    private readonly MainWindowViewModel _mainWindowViewModel;

    private readonly static CancellationTokenSource _cts = new();
    private readonly static CancellationToken _stoppingToken = _cts.Token;

    public MainWindow(
        PipeService pipeService,
        SettingsPage settingsPage,
        MainWindowViewModel mainWindowViewModel
    )
    {
        _pipeService = pipeService;
        _settingsPage = settingsPage;
        _mainWindowViewModel = mainWindowViewModel;

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
                var status = await _pipeService.QueryBackgroundServiceStatus(_stoppingToken);
                if (status != QueryStatus.LOCKED)
                {
                    _mainWindowViewModel.StatusBackgroundService = QueryStatusToBool(status);
                }
                await Task.Delay(10_000, _stoppingToken);
            }
        }, _stoppingToken);

        _ = Task.Run(async () =>
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                var status = await _pipeService.QueryWebSocketStatus(_stoppingToken);
                if (status != QueryStatus.LOCKED)
                {
                    _mainWindowViewModel.StatusWebSocket = QueryStatusToBool(status);
                }
                await Task.Delay(30_000, _stoppingToken);
            }
        }, _stoppingToken);

        _ = Task.Run(async () =>
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                var status = await _pipeService.QueryDentrixStatus(_stoppingToken);
                if (status != QueryStatus.LOCKED)
                {
                    _mainWindowViewModel.StatusDentrix = QueryStatusToBool(status);
                }
                await Task.Delay(30_000, _stoppingToken);
            }
        }, _stoppingToken);
    }

    private static bool? QueryStatusToBool(QueryStatus status)
    {
        switch (status)
        {
            case QueryStatus.SUCCESS:
                {
                    return true;
                }
            case QueryStatus.FAILURE:
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