using FluidicML.Gain.Extensions;
using FluidicML.Gain.Hosting;
using FluidicML.Gain.Views;
using FluidicML.Gain.ViewModels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using Serilog;

namespace FluidicML.Gain;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static IConfiguration _configService { get; } =
        new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
#if Debug
            .AddJsonFile("appsettings.Production.json")
#elif Staging
            .AddJsonFile("appsettings.Staging.json")
#elif Release
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
            .ConfigureLogging(c =>
            {
                // Do not change the `EventLog` source value specified in the `appsettings.json`
                // file. Once the application logs to a particular source once, Windows will
                // raise an exception if it attempts to log elsewhere. This choice of initial source
                // is specified within the registry.
                //
                // The choice of `Application` is generally a safe default considering it will
                // already exist on all Windows installations (meaning we can avoid mucking around
                // with necessary permissions needed to create a new source). If gung ho on converting,
                // refer to https://stackoverflow.com/a/51232566 on how to reset. You'll likely want
                // to incorporate these steps into an update patch of the installer.
                var logger = new LoggerConfiguration()
                    .ReadFrom.Configuration(_configService)
                    .CreateLogger();

                c.AddSerilog(logger);
            })
            .ConfigureServices(
                (_1, services) =>
                {
                    _ = services.AddHostedService<ApplicationHostService>();
                    _ = services.AddSingleton(_configService);
                    _ = services.AddSingleton<PipeService>();
                    _ = services.AddSingleton<RegistryService>();
                    _ = services.AddSingleton<DentrixService>();
                    _ = services.AddView<SettingsPage, SettingsViewModel>();
                    _ = services.AddSingleton<MainWindowViewModel>();
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
