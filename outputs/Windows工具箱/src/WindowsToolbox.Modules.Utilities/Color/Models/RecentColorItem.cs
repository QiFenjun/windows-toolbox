using System.Windows.Media;

namespace WindowsToolbox.Modules.Utilities.Color.Models;

public sealed record RecentColorItem
{
    public RecentColorItem(ColorValue value)
    {
        Value = value;
        Brush = new SolidColorBrush(value.ToMediaColor());
        Brush.Freeze();
    }

    public ColorValue Value { get; }
    public string Hex => Value.ToHex();
    public SolidColorBrush Brush { get; }
}
