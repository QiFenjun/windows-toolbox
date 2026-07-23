using System.Globalization;
using System.Windows.Data;

namespace WindowsToolbox.App.Converters;

public sealed class IconKeyToGlyphConverter : IValueConverter
{
    private static readonly IReadOnlyDictionary<string, string> Glyphs =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = "\uE80F",
            ["Power"] = "\uE7E8",
            ["Timer"] = "\uE916",
            ["Settings"] = "\uE713",
            ["Info"] = "\uE946",
            ["Search"] = "\uE721",
            ["Theme"] = "\uE706",
            ["Menu"] = "\uE700",
            ["Toolbox"] = "\uE90F",
            ["Recent"] = "\uE823"
        };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key && Glyphs.TryGetValue(key, out string? glyph) ? glyph : "\uE90F";

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
