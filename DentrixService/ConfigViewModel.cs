using CommunityToolkit.Mvvm.ComponentModel;

namespace DentrixService;

public partial class ConfigViewModel(IConfiguration configService) : ObservableObject
{
    private readonly IConfiguration _configService = configService;

    [ObservableProperty]
    private string _apiKey = String.Empty;

    public Uri ApiUrl
    {
        get => new(_configService.GetValue<string>("API_URL")!);
    }

    public Uri WsUrl
    {
        get => new(_configService.GetValue<string>("WS_URL")!);
    }
}
