using System.Globalization;
using System.Windows.Data;

namespace WindowsToolbox.App.Converters;

public sealed class BooleanToPauseTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "继续监听" : "暂停监听";
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
