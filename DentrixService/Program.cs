using DentrixService;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddHostedService<WindowsBackgroundService>();

var host = builder.Build();
host.Run();
