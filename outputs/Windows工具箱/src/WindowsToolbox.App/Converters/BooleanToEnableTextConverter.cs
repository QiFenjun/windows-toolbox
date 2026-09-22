using System.Globalization;
using System.Windows.Data;

namespace WindowsToolbox.App.Converters;

public sealed class BooleanToEnableTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? "关闭监听" : "启用监听";
    public object ConvertBack(object value, Type targetType, object? parameter, CultureInfo culture) =>
        Binding.DoNothing;
}
