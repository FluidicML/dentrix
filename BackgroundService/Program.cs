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
builder.Services.AddSingleton<IConfiguration>(configService);
builder.Services.AddSingleton<ConfigProxy>();
builder.Services.AddSingleton<DatabaseAdapter>();

var host = builder.Build();
host.Run();