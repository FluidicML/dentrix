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
}
