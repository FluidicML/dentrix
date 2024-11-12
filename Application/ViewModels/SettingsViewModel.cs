using CommunityToolkit.Mvvm.ComponentModel;
using System.Windows;

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

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageVisibility))]
    private string _message;

    public Visibility MessageVisibility =>
        string.IsNullOrEmpty(Message) ? Visibility.Collapsed : Visibility.Visible;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MessageColor))]
    private bool _isError;

    public string MessageColor =>
        IsError ? "#dc2626" : "#10b981";

    public SettingsViewModel()
    {
        _apiKey = string.Empty;
        _message = string.Empty;
        _isError = false;
        _isDirty = false;
        _isLoading = false;
    }
}
