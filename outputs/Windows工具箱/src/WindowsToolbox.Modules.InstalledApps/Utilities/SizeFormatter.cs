namespace WindowsToolbox.Modules.InstalledApps.Utilities;

public static class SizeFormatter
{
    private static readonly string[] Units = ["B", "KB", "MB", "GB", "TB"];

    public static string Format(long? bytes)
    {
        if (!bytes.HasValue || bytes.Value < 0)
            return "未知";

        double value = bytes.Value;
        int unit = 0;
        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0
            ? $"{value:0} {Units[unit]}"
            : $"{value:0.##} {Units[unit]}";
    }
}
