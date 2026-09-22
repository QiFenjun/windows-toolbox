using System.Text;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public static class ClipboardOptions
{
    public const int DefaultCapacity = 300;
    public const int DefaultRetentionDays = 30;
    public const int MaxItemBytes = 256 * 1024;
    public static readonly int[] Capacities = [100, 300, 500, 1000];
    public static readonly int[] RetentionDays = [1, 7, 30, 90, 0];

    public static bool IsValidText(string? text) =>
        !string.IsNullOrEmpty(text) && Encoding.Unicode.GetByteCount(text) <= MaxItemBytes;
}
