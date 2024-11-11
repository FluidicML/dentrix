using CommunityToolkit.Mvvm.ComponentModel;

namespace DentrixUI.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _apiKey;

    public SettingsViewModel()
    {
        _apiKey = string.Empty;
    }
}
