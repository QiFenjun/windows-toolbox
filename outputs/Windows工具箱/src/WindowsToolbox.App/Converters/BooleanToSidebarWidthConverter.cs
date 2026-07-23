using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WindowsToolbox.App.Converters;

public sealed class BooleanToSidebarWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        new GridLength(value is true ? 232 : 72);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
