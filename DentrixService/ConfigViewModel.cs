using CommunityToolkit.Mvvm.ComponentModel;

namespace DentrixService;

public partial class ConfigViewModel(IConfiguration configService) : ObservableObject
{
    [ObservableProperty]
    private string _apiKey = String.Empty;

    public Uri ApiUrl
    {
        get => new(configService.GetValue<string>("API_URL")!);
    }

    public Uri WsUrl
    {
        get => new(configService.GetValue<string>("WS_URL")!);
    }
}
