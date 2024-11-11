using DentrixService;

var configService = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
#if DEBUG
    .AddJsonFile("appsettings.Staging.json")
#else
    .AddJsonFile("appsettings.Production.json")
#endif
    .Build();

var settings = new Settings(configService);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddHostedService<WindowsBackgroundService>();

builder.Services.AddSingleton(configService);
builder.Services.AddSingleton(settings);

builder.Services.AddSingleton(new HttpClient()
{
    BaseAddress = settings.ApiUrl,
    DefaultRequestHeaders =
    {
        { "Authorization", $"Api {settings.ApiKey}" }
    }
});

builder.Services.AddSingleton<DatabaseAdapter>();

var host = builder.Build();
host.Run();
