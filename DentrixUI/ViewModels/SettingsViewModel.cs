using CommunityToolkit.Mvvm.ComponentModel;

namespace DentrixUI.ViewModels;

public partial class SettingsViewModel : BaseViewModel
{
    [ObservableProperty]
    private string _apiKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveButtonIsEnabled))]
    private bool _isDirty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveButtonIsEnabled))]
    private bool _isLoading;

    public bool SaveButtonIsEnabled => IsDirty && !IsLoading;

    public SettingsViewModel()
    {
        _apiKey = string.Empty;
        _isDirty = false;
        _isLoading = false;
    }
}
