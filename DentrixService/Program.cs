using DentrixService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WindowsBackgroundService>();
builder.Services.AddSingleton<DatabaseAdapter>();

var host = builder.Build();
host.Run();
