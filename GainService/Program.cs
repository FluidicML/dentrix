using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;
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

        builder.Services.AddWindowsService();

        builder.Services.AddLogging(builder =>
        {
            var logger = new LoggerConfiguration()
                .ReadFrom.Configuration(config)
                .CreateLogger();

            builder.AddSerilog(logger);
        });

        builder.Services.AddHostedService<WindowsBackgroundService>();
        builder.Services.AddSingleton<IConfiguration>(config);
        builder.Services.AddSingleton<DentrixAdapter>();
        builder.Services.AddSingleton<SocketAdapter>();
        builder.Services.AddSingleton<PipeAdapter>();

        var host = builder.Build();
        host.Run();
    }
}
