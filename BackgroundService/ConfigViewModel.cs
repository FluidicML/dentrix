using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.IO.IsolatedStorage;

namespace FluidicML.Gain;

public partial class ConfigViewModel(IConfiguration _configService) : ObservableObject
{
    private static readonly string PERSISTED_FILENAME = "Config.data";
    private static readonly string API_KEY = "API_KEY";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(WriteApiKey))]
    private string _apiKey = ReadApiKey() ?? string.Empty;

    [RelayCommand]
    private async Task WriteApiKey()
    {
        await Task.Run(() =>
        {
            IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();
            using IsolatedStorageFileStream stream = storage.OpenFile(PERSISTED_FILENAME, FileMode.Create, FileAccess.Write);
            using StreamWriter writer = new(stream);

            var encodedAccessToken = System.Text.Encoding.UTF8.GetBytes(ApiKey);
            writer.WriteLine("{0},{1}", API_KEY, Convert.ToBase64String(encodedAccessToken));
        });
    }

    private static string? ReadApiKey()
    {
        IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();

        try
        {
            if (storage.FileExists(PERSISTED_FILENAME))
            {
                using IsolatedStorageFileStream stream = storage.OpenFile(PERSISTED_FILENAME, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);
                {
                    while (!reader.EndOfStream)
                    {
                        // The value is base64 encoded so no comma can appear.
                        string[]? kv = reader.ReadLine()?.Split([',']);
                        if (kv is not null)
                        {
                            if (kv[0] == API_KEY)
                            {
                                var encoded = Convert.FromBase64String(kv[1]);
                                return System.Text.Encoding.UTF8.GetString(encoded);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"{ex.Message}\n");
        }

        return null;
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
