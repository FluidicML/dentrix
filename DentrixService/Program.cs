using DentrixService;

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
#if DEBUG
    .AddJsonFile("appsettings.Staging.json")
#else
    .AddJsonFile("appsettings.Production.json")
#endif
    .Build();

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WindowsBackgroundService>();
builder.Services.AddSingleton<IConfiguration>(config);
builder.Services.AddSingleton(new HttpClient()
{
    BaseAddress = new Uri("https://api.usegain.ai"),
    DefaultRequestHeaders =
    {
        { "Authorization", $"Api {config.GetValue<string>("API_KEY")}" }
    }
});
builder.Services.AddSingleton<DatabaseAdapter>();

var host = builder.Build();
host.Run();
