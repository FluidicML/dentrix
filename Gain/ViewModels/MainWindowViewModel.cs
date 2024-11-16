using CommunityToolkit.Mvvm.ComponentModel;

namespace FluidicML.Gain.ViewModels;

public partial class MainWindowViewModel : BaseViewModel
{
    // Null indicates an indeterminate value.

    /// <summary>
    /// Whether or not the background service is reachable.
    /// </summary>
    [ObservableProperty]
    private bool? _statusBackgroundService = null;

    /// <summary>
    /// Whether or not the background service reports its connected to Gain servers.
    /// </summary>
    [ObservableProperty]
    private bool? _statusWebSocket = null;

    /// <summary>
    /// Whether or not the background service reports its connected to Dentrix's database.
    /// </summary>
    [ObservableProperty]
    private bool? _statusDentrix = null;
}
