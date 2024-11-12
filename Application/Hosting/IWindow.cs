using System.Windows;

namespace FluidicML.Gain.Hosting;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}