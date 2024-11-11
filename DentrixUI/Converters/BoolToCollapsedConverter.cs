using System.Globalization;
using System.Windows.Data;
using System.Windows;

namespace DentrixUI.Converters;

public class BoolToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool original = (bool)value;
        return original ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        Visibility original = (Visibility)value;
        return original == Visibility.Visible;
    }
}