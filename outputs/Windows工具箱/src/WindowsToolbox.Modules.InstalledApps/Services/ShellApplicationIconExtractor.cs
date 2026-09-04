using System.Runtime.InteropServices;
using System.IO;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>通过 Windows Shell 提取 EXE、DLL、ICO 和快捷方式的图标资源。</summary>
public sealed class ShellApplicationIconExtractor : IApplicationIconExtractor
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiLargeIcon = 0x000000000;

    public ImageSource? Extract(string sourcePath, int? iconIndex, int desiredSize)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(sourcePath))
            return null;

        IntPtr largeIcon = IntPtr.Zero;
        IntPtr smallIcon = IntPtr.Zero;
        try
        {
            int result = SHDefExtractIcon(
                sourcePath,
                iconIndex ?? 0,
                0,
                out largeIcon,
                out smallIcon,
                (uint)(desiredSize | (desiredSize << 16)));
            if (result >= 0 && largeIcon != IntPtr.Zero)
                return CreateFrozenImage(largeIcon);

            return ExtractAssociatedIcon(sourcePath);
        }
        catch (Exception)
        {
            return null;
        }
        finally
        {
            if (largeIcon != IntPtr.Zero)
                DestroyIcon(largeIcon);
            if (smallIcon != IntPtr.Zero)
                DestroyIcon(smallIcon);
        }
    }

    private static ImageSource? ExtractAssociatedIcon(string sourcePath)
    {
        SHFILEINFO info = new();
        IntPtr result = SHGetFileInfo(
            sourcePath,
            0,
            ref info,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            ShgfiIcon | ShgfiLargeIcon);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            return null;

        try { return CreateFrozenImage(info.hIcon); }
        finally { DestroyIcon(info.hIcon); }
    }

    private static BitmapSource CreateFrozenImage(IntPtr iconHandle)
    {
        BitmapSource bitmap = Imaging.CreateBitmapSourceFromHIcon(
            iconHandle,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());
        if (bitmap.CanFreeze)
            bitmap.Freeze();
        return bitmap;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHDefExtractIcon(
        string pszFile,
        int iIndex,
        uint uFlags,
        out IntPtr phiconLarge,
        out IntPtr phiconSmall,
        uint nIconSize);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath,
        uint dwFileAttributes,
        ref SHFILEINFO psfi,
        uint cbFileInfo,
        uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }
}
