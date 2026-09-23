using System.Globalization;
using WindowsToolbox.Modules.Utilities.Color.Models;

namespace WindowsToolbox.Modules.Utilities.Color.Services;

public static class ColorConversionService
{
    public static bool TryParseHex(string? text, out ColorValue color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(text))
            return false;
        string value = text.Trim();
        if (value.StartsWith('#'))
            value = value[1..];
        try
        {
            switch (value.Length)
            {
                case 3:
                    color = new(byte.MaxValue,
                        Expand(value[0]), Expand(value[1]), Expand(value[2]));
                    return true;
                case 6:
                    color = new(byte.MaxValue,
                        ParseByte(value.AsSpan(0, 2)), ParseByte(value.AsSpan(2, 2)), ParseByte(value.AsSpan(4, 2)));
                    return true;
                case 8:
                    color = new(
                        ParseByte(value.AsSpan(0, 2)), ParseByte(value.AsSpan(2, 2)),
                        ParseByte(value.AsSpan(4, 2)), ParseByte(value.AsSpan(6, 2)));
                    return true;
                default:
                    return false;
            }
        }
        catch (FormatException) { return false; }
    }

    public static ColorValue FromRgb(int red, int green, int blue, byte alpha = byte.MaxValue)
    {
        if (red is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(red));
        if (green is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(green));
        if (blue is < 0 or > 255) throw new ArgumentOutOfRangeException(nameof(blue));
        return new ColorValue(alpha, (byte)red, (byte)green, (byte)blue);
    }

    public static HslColor ToHsl(ColorValue color)
    {
        double red = color.R / 255d;
        double green = color.G / 255d;
        double blue = color.B / 255d;
        double max = Math.Max(red, Math.Max(green, blue));
        double min = Math.Min(red, Math.Min(green, blue));
        double delta = max - min;
        double lightness = (max + min) / 2d;
        double hue = 0d;
        double saturation = 0d;

        if (delta != 0d)
        {
            saturation = delta / (1d - Math.Abs(2d * lightness - 1d));
            if (max == red)
                hue = 60d * (((green - blue) / delta) % 6d);
            else if (max == green)
                hue = 60d * (((blue - red) / delta) + 2d);
            else
                hue = 60d * (((red - green) / delta) + 4d);
            if (hue < 0d)
                hue += 360d;
        }

        return new HslColor(hue, saturation * 100d, lightness * 100d);
    }

    public static ColorValue FromHsl(double hue, double saturation, double lightness, byte alpha = byte.MaxValue)
    {
        if (!double.IsFinite(hue) || hue is < 0d or > 360d)
            throw new ArgumentOutOfRangeException(nameof(hue));
        if (!double.IsFinite(saturation) || saturation is < 0d or > 100d)
            throw new ArgumentOutOfRangeException(nameof(saturation));
        if (!double.IsFinite(lightness) || lightness is < 0d or > 100d)
            throw new ArgumentOutOfRangeException(nameof(lightness));

        double h = hue == 360d ? 0d : hue;
        double s = saturation / 100d;
        double l = lightness / 100d;
        double chroma = (1d - Math.Abs(2d * l - 1d)) * s;
        double x = chroma * (1d - Math.Abs((h / 60d % 2d) - 1d));
        double m = l - chroma / 2d;
        (double r, double g, double b) = h switch
        {
            < 60d => (chroma, x, 0d),
            < 120d => (x, chroma, 0d),
            < 180d => (0d, chroma, x),
            < 240d => (0d, x, chroma),
            < 300d => (x, 0d, chroma),
            _ => (chroma, 0d, x)
        };
        return new ColorValue(alpha, ToByte((r + m) * 255d), ToByte((g + m) * 255d), ToByte((b + m) * 255d));
    }

    public static bool TryParseByte(string? text, out byte value) =>
        byte.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);

    public static bool TryParsePercent(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) && value is >= 0d and <= 100d;

    public static bool TryParseHue(string? text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
        double.IsFinite(value) && value is >= 0d and <= 360d;

    private static byte Expand(char digit) => checked((byte)(byte.Parse(digit.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 17));

    private static byte ParseByte(ReadOnlySpan<char> pair) =>
        byte.Parse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static byte ToByte(double value) => checked((byte)Math.Clamp(
        (int)Math.Round(value, MidpointRounding.AwayFromZero), 0, 255));
}
