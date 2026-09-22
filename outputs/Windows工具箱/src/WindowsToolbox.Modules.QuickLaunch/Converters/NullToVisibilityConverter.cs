using System.Globalization;
using System.Windows;
using WpfBinding = System.Windows.Data.Binding;
using WpfIValueConverter = System.Windows.Data.IValueConverter;

namespace WindowsToolbox.Modules.QuickLaunch.Converters;

public sealed class NullToVisibilityConverter : WpfIValueConverter
{
    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool isNull = value is null;
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => WpfBinding.DoNothing;
}
