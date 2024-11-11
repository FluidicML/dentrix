namespace DentrixService;

public sealed class Config
{
    private readonly IConfiguration _configService;

    public Config(IConfiguration configService)
    {
        _configService = configService;
    }

    public string? ApiKey
    {
        get => null;
    }

    public Uri ApiUrl
    {
        get => new(_configService.GetValue<string>("API_URL")!);
    }

    public Uri WsUrl
    {
        get => new(_configService.GetValue<string>("WS_URL")!);
    }
}
