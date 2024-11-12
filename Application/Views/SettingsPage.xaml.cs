using DentrixUI.ViewModels;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace DentrixUI.Views;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    private static readonly string NAMED_PIPE_SERVER = "gain-dentrix";

    private readonly Uri _baseAddress;
    private readonly HttpClient _httpClient;

    public SettingsViewModel ViewModel { get; }

    public SettingsPage(
        IConfiguration configService,
        SettingsViewModel settingsPageViewModel
    )
    {
        _baseAddress = new Uri(configService.GetValue<string>("API_URL")!);
        _httpClient = new HttpClient() { BaseAddress = _baseAddress };

        ViewModel = settingsPageViewModel;

        DataContext = this;
        InitializeComponent();

        // https://github.com/lepoco/wpfui/blob/950ade69bd123f605b507dc472796cb6ef9bfd59/src/Wpf.Ui/Controls/PasswordBox/PasswordBox.xaml#L186
        ApiKeyPasswordBox.Resources["TextControlPlaceholderForeground"] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString("#44F9FAFB"));
    }

    private void SettingsPage_ApiKeyChanged(object sender, EventArgs e)
    {
        ViewModel.ApiKey = ((PasswordBox)sender).Password;
        ViewModel.Message = String.Empty;
        ViewModel.IsError = false;
        ViewModel.IsDirty = true;
    }

    private async void SettingsPage_SaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            ViewModel.IsLoading = true;

            using HttpRequestMessage request = new HttpRequestMessage()
            {
                RequestUri = new Uri(_baseAddress, "/api/dentrix/health"),
                Method = HttpMethod.Get,
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Api", ViewModel.ApiKey);

            using HttpResponseMessage response = await _httpClient.SendAsync(request);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                ViewModel.Message = "Invalid API key.";
                ViewModel.IsError = true;
            }
            else
            {
                response.EnsureSuccessStatusCode();

                await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.Out);
                await pipeClient.ConnectAsync();

                using var writer = new StreamWriter(pipeClient);
                await writer.WriteAsync($"Api {ViewModel.ApiKey}\n");
                await writer.FlushAsync();
                writer.Close();

                ViewModel.Message = "Success.";
                ViewModel.IsError = false;
                ViewModel.IsDirty = false;
            }
        }
        catch (Exception)
        {
            ViewModel.Message = "Encountered an unknown error. Please try again later.";
            ViewModel.IsError = true;
        }
        finally
        {
            ViewModel.IsLoading = false;
        }
    }
}
