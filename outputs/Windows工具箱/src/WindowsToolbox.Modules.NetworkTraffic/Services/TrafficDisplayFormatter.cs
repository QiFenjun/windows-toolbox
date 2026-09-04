namespace WindowsToolbox.Modules.NetworkTraffic.Services;

public static class TrafficDisplayFormatter
{
    public static string Bytes(long bytes) => Format(Math.Max(0, bytes), string.Empty);
    public static string Rate(double bytesPerSecond) => Format(Math.Max(0, bytesPerSecond), "/s");

    private static string Format(double value, string suffix)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string digits = unit == 0 ? "0" : value >= 100 ? "0" : "0.0";
        return $"{value.ToString(digits, System.Globalization.CultureInfo.CurrentCulture)} {units[unit]}{suffix}";
    }
}
