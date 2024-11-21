using Serilog;

namespace FluidicML.Gain;

public class Program
{
    private static readonly IConfiguration config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
#if Debug
        .AddJsonFile("appsettings.Debug.json")
#elif Staging
        .AddJsonFile("appsettings.Staging.json")
#elif Release
        .AddJsonFile("appsettings.Production.json")
#endif
        .Build();

    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);

        builder
            .Services
            .AddWindowsService()
            .AddLogging(c =>
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
                    .ReadFrom.Configuration(config)
                    .CreateLogger();

                c.AddSerilog(logger);
            })
            .AddHostedService<WindowsBackgroundService>()
            .AddSingleton<IConfiguration>(config)
            .AddSingleton<DentrixAdapter>()
            .AddSingleton<SocketAdapter>()
            .AddSingleton<PipeAdapter>();

        var host = builder.Build();
        host.Run();
    }
}
