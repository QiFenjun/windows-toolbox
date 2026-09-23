using MediaColor = System.Windows.Media.Color;

namespace WindowsToolbox.Modules.Utilities.Color.Models;

public readonly record struct ColorValue(byte A, byte R, byte G, byte B)
{
    public bool IsOpaque => A == byte.MaxValue;

    public string ToHex() => IsOpaque
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public string ToArgbHex() => $"#{A:X2}{R:X2}{G:X2}{B:X2}";

    public MediaColor ToMediaColor() => MediaColor.FromArgb(A, R, G, B);

    public static ColorValue FromMediaColor(MediaColor color) => new(color.A, color.R, color.G, color.B);
}
