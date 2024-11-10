using System.Windows;

namespace DentrixUI.Hosting;

public interface IWindow
{
    event RoutedEventHandler Loaded;

    void Show();
}