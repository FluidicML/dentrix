using FluidicML.Gain.Extensions;
using FluidicML.Gain.Hosting;
using FluidicML.Gain.Views;
using FluidicML.Gain.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;

namespace FluidicML.Gain;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IConfiguration _configService { get; } =
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
#if DEBUG
            .AddJsonFile("appsettings.Staging.json")
#else
            .AddJsonFile("appsettings.Production.json")
#endif
        .Build();

    private static IHost? _host = null;

    /// <summary>
    /// Occurs when the application is loading.
    /// </summary>
    private void App_Startup(object sender, StartupEventArgs e)
    {
        _host = Host.CreateDefaultBuilder()
            .ConfigureAppConfiguration(c =>
            {
                _ = c.SetBasePath(AppContext.BaseDirectory);
            })
            .ConfigureServices(
                (_1, services) =>
                {
                    _ = services.AddHostedService<ApplicationHostService>();
                    _ = services.AddSingleton(_configService);
                    _ = services.AddView<SettingsPage, SettingsViewModel>();
                    _ = services.AddSingleton<IWindow, MainWindow>();
                }
            )
            .Build();

        _host.Start();
    }

    /// <summary>
    /// Occurs when the application is closing.
    /// </summary>
    private void App_Exit(object sender, ExitEventArgs e)
    {
        if (_host == null)
        {
            return;
        }

        _host.StopAsync().Wait();
        _host.Dispose();
        _host = null;
    }
}
