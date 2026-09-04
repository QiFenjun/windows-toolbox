using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>源文件变更时自动产生新键，旧的内存条目可由进程生命周期自然释放。</summary>
public static class ApplicationIconCacheKey
{
    public static string Create(string sourcePath, int? iconIndex, int desiredSize)
    {
        DateTime lastWriteUtc = GetLastWriteUtc(sourcePath);
        string identity = string.Join(
            "|",
            sourcePath.Trim().ToUpperInvariant(),
            iconIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
            desiredSize.ToString(CultureInfo.InvariantCulture),
            lastWriteUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    private static DateTime GetLastWriteUtc(string sourcePath)
    {
        try { return File.Exists(sourcePath) ? File.GetLastWriteTimeUtc(sourcePath) : DateTime.MinValue; }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException) { return DateTime.MinValue; }
    }
}
