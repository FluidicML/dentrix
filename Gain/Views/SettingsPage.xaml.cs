using FluidicML.Gain.Hosting;
using FluidicML.Gain.ViewModels;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Windows.Media;
using Wpf.Ui.Controls;

namespace FluidicML.Gain.Views;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    private readonly Uri _baseAddress;
    private readonly HttpClient _httpClient;

    public SettingsViewModel ViewModel { get; }
    public MainWindowViewModel MainWindowViewModel { get; }

    public SettingsPage(
        IConfiguration configService,
        SettingsViewModel settingsViewModel,
        MainWindowViewModel mainWindowViewModel
    )
    {
        _baseAddress = new Uri(configService.GetValue<string>("ApiUrl")!);
        _httpClient = new HttpClient() { BaseAddress = _baseAddress };

        ViewModel = settingsViewModel;
        MainWindowViewModel = mainWindowViewModel;

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

            using var request = new HttpRequestMessage()
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

                await PipeService.SendApiKey(ViewModel.ApiKey);

                ViewModel.Message = "Success.";
                ViewModel.IsError = false;
                ViewModel.IsDirty = false;
            }
        }
        catch (TimeoutException)
        {
            ViewModel.Message = "Could not connect to Dentrix service.";
            ViewModel.IsError = true;
        }
        catch (Exception exc)
        {
            ViewModel.Message = exc.Message;
            ViewModel.IsError = true;
        }
        finally
        {
            ViewModel.IsLoading = false;
        }
    }
}
