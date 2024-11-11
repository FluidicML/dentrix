using DentrixUI.ViewModels;
using Wpf.Ui.Controls;

namespace DentrixUI.Views;

/// <summary>
/// Interaction logic for Settings.xaml
/// </summary>
public partial class SettingsPage : INavigableView<SettingsViewModel>
{
    public SettingsViewModel ViewModel { get; }

    public SettingsPage(SettingsViewModel settingsPageViewModel)
    {
        ViewModel = settingsPageViewModel;

        DataContext = this;
        InitializeComponent();
    }

    private void SettingsPage_ApiKeyChanged(object sender, EventArgs e)
    {
        ViewModel.ApiKey = ((PasswordBox)sender).Password;
        ViewModel.IsDirty = true;
    }

    private async void SettingsPage_SaveClick(object sender, System.Windows.RoutedEventArgs e)
    {
        try
        {
            ViewModel.IsLoading = true;
        }
        finally
        {
            ViewModel.IsLoading = false;
        }
    }
}
