using System.ComponentModel;
using System.Runtime.InteropServices;
using MediaColor = System.Windows.Media.Color;
using WindowsToolbox.Modules.Utilities.Color.Interop;
using WindowsToolbox.Modules.Utilities.Color.Models;

namespace WindowsToolbox.Modules.Utilities.Color.Services;

public sealed class WindowsScreenColorSampler : IScreenColorSampler
{
    public ScreenPoint GetCursorPosition()
    {
        if (!ScreenInterop.GetCursorPos(out ScreenInterop.Point point))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        return point.ToScreenPoint();
    }

    public MediaColor Sample(ScreenPoint point)
    {
        nint dc = ScreenInterop.GetDC(0);
        if (dc == 0)
            throw new Win32Exception(Marshal.GetLastWin32Error());
        try
        {
            uint colorRef = ScreenInterop.GetPixel(dc, point.X, point.Y);
            if (colorRef == uint.MaxValue)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "读取屏幕像素失败。");
            return MediaColor.FromRgb((byte)(colorRef & 0xFF), (byte)((colorRef >> 8) & 0xFF), (byte)((colorRef >> 16) & 0xFF));
        }
        finally
        {
            if (ScreenInterop.ReleaseDC(0, dc) == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error(), "释放屏幕设备上下文失败。");
        }
    }
}
