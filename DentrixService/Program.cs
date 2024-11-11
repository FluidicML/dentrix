using DentrixService;

var configService = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
#if DEBUG
    .AddJsonFile("appsettings.Staging.json")
#else
    .AddJsonFile("appsettings.Production.json")
#endif
    .Build();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WindowsBackgroundService>();
builder.Services.AddSingleton(configService);
builder.Services.AddSingleton<ConfigViewModel>();
builder.Services.AddSingleton<DatabaseAdapter>();

var host = builder.Build();
host.Run();
