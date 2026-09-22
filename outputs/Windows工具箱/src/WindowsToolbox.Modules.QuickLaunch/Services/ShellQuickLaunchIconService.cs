using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

/// <summary>Quick Launch 本地 Shell 图标缓存；不依赖 InstalledApps 模块。</summary>
public sealed class ShellQuickLaunchIconService : IQuickLaunchIconService
{
    private readonly ConcurrentDictionary<string, Task<ImageSource?>> _cache = new(StringComparer.OrdinalIgnoreCase);

    public Task<ImageSource?> GetAsync(QuickLaunchItem item, int size = 40, CancellationToken cancellationToken = default)
    {
        if (item.Type == QuickLaunchItemType.Url || !QuickLaunchItemRules.TargetExists(item))
            return Task.FromResult<ImageSource?>(null);
        string key = $"{item.Type}|{item.Target}|{size}";
        return _cache.GetOrAdd(key, _ => Task.Run(() => Extract(item.Target), cancellationToken));
    }

    private static ImageSource? Extract(string path)
    {
        if (!OperatingSystem.IsWindows()) return null;
        SHFILEINFO info = new();
        IntPtr result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero) return null;
        try
        {
            BitmapSource image = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            image.Freeze();
            return image;
        }
        finally { DestroyIcon(info.hIcon); }
    }

    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string szTypeName;
    }
}
