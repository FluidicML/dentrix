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
            .AddWindowsService(c =>
            {
                c.ServiceName = "Gain Service";
            })
            .AddLogging(c =>
            {
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
