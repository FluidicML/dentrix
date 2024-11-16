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
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

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

        // Periodically poll for status updates.
        var poll = new Task(async () =>
        {
            while (true)
            {
                try
                {
                    await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
                    await pipeClient.ConnectAsync(2500); // Milliseconds timeout

                    ViewModel.StatusService = true;

                    var writer = new StreamWriter(pipeClient);
                    var reader = new StreamReader(pipeClient);

                    await writer.WriteLineAsync("Status");
                    await writer.FlushAsync();

                    var status = await reader.ReadLineAsync();
                    if (status != null)
                    {
                        foreach (string kv in status.Split(","))
                        {
                            if (kv.StartsWith("Ws="))
                            {
                                ViewModel.StatusWebSocket = kv == "Ws=1";
                            }
                            else if (kv.StartsWith("Db="))
                            {
                                ViewModel.StatusDatabase = kv == "Db=1";
                            }
                        }
                    }
                }
                catch (TimeoutException)
                {
                    ViewModel.StatusService = false;
                    ViewModel.StatusWebSocket = null;
                    ViewModel.StatusDatabase = null;
                }
                catch (Exception)
                {
                    ViewModel.StatusService = null;
                    ViewModel.StatusWebSocket = null;
                    ViewModel.StatusDatabase = null;
                }

                await Task.Delay(2500);
            }
        });

        poll.Start();
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

                await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.Out);
                await pipeClient.ConnectAsync(2500); // Milliseconds

                using var writer = new StreamWriter(pipeClient);
                await writer.WriteLineAsync($"Api {ViewModel.ApiKey}");
                await writer.FlushAsync();

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
