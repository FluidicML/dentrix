using Microsoft.Extensions.Logging.Configuration;
using Microsoft.Extensions.Logging.EventLog;

namespace FluidicML.Gain;

public class Program
{
    private static readonly IConfiguration configService = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
#if DEBUG
        .AddJsonFile("appsettings.Staging.json")
#else
        .AddJsonFile("appsettings.Production.json")
#endif
        .Build();

    public static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
#if !DEBUG
        builder.Services.AddWindowsService(options =>
        {
            options.ServiceName = "Gain Service - Dentrix Adapter";
        });
#endif

        LoggerProviderOptions.RegisterProviderOptions<
            EventLogSettings, EventLogLoggerProvider>(builder.Services);

        builder.Services.AddHostedService<WindowsBackgroundService>();
        builder.Services.AddSingleton<IConfiguration>(configService);
        builder.Services.AddSingleton<DentrixAdapter>();
        builder.Services.AddSingleton<SocketAdapter>();
        builder.Services.AddSingleton<PipeAdapter>();

        var host = builder.Build();
        host.Run();
    }
}
