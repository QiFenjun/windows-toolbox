using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using WindowsToolbox.Modules.WindowTools.Interop;
using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

/// <summary>唯一接触 Win32 的窗口平台实现；所有尺寸均为外框物理像素。</summary>
public sealed class WindowsWindowPlatform : IWindowPlatform
{
    private const uint MonitorDefaultToNearest = 2;
    private const uint MonitorInfoPrimary = 1;

    public IReadOnlyList<WindowNativeSnapshot> EnumerateTopLevelWindows()
    {
        if (!OperatingSystem.IsWindows()) return [];
        List<WindowNativeSnapshot> windows = [];
        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (TryGetWindow(hwnd, out WindowNativeSnapshot snapshot)) windows.Add(snapshot);
            return true;
        }, 0);
        return windows;
    }

    public bool TryGetWindow(nint hwnd, out WindowNativeSnapshot snapshot)
    {
        snapshot = default!;
        if (!OperatingSystem.IsWindows() || hwnd == 0 || !NativeMethods.IsWindow(hwnd)) return false;
        if (!NativeMethods.GetWindowRect(hwnd, out NativeMethods.RECT outer)) return false;

        uint processId;
        NativeMethods.GetWindowThreadProcessId(hwnd, out processId);
        if (processId == 0) return false;

        string title = ReadText(hwnd, NativeMethods.GetWindowTextLengthW, NativeMethods.GetWindowTextW);
        StringBuilder className = new(256);
        NativeMethods.GetClassNameW(hwnd, className, className.Capacity);
        WindowRect client = ReadClientRect(hwnd);
        nint monitor = NativeMethods.MonitorFromWindow(hwnd, MonitorDefaultToNearest);
        snapshot = new WindowNativeSnapshot(
            hwnd,
            title,
            processId,
            ReadProcessName(processId, out string? executablePath),
            executablePath,
            className.ToString(),
            ToRect(outer),
            client,
            NativeMethods.IsWindowVisible(hwnd),
            NativeMethods.IsIconic(hwnd) ? WindowStateKind.Minimized : NativeMethods.IsZoomed(hwnd) ? WindowStateKind.Maximized : WindowStateKind.Normal,
            (NativeMethods.GetWindowLongPtr(hwnd, NativeMethods.GwlExStyle).ToInt64() & NativeMethods.WsExTopMost) != 0,
            monitor,
            ReadDpi(hwnd));
        return true;
    }

    public IReadOnlyList<MonitorSnapshot> EnumerateMonitors()
    {
        if (!OperatingSystem.IsWindows()) return [];
        List<MonitorSnapshot> monitors = [];
        NativeMethods.EnumDisplayMonitors(0, 0, (monitor, _, _, _) =>
        {
            NativeMethods.MONITORINFOEX info = new() { cbSize = Marshal.SizeOf<NativeMethods.MONITORINFOEX>() };
            if (NativeMethods.GetMonitorInfoW(monitor, ref info))
            {
                WindowRect bounds = ToRect(info.rcMonitor);
                monitors.Add(new MonitorSnapshot(
                    monitor,
                    info.szDevice,
                    $"Display {monitors.Count + 1}",
                    bounds,
                    ToRect(info.rcWork),
                    (info.dwFlags & MonitorInfoPrimary) != 0,
                    96));
            }
            return true;
        }, 0);
        return monitors;
    }

    public nint GetForegroundWindow() => OperatingSystem.IsWindows() ? NativeMethods.GetForegroundWindow() : 0;

    public bool TryRestore(nint hwnd, out int errorCode)
    {
        errorCode = 0;
        if (!OperatingSystem.IsWindows() || !NativeMethods.IsWindow(hwnd)) { errorCode = 1400; return false; }
        NativeMethods.ShowWindow(hwnd, NativeMethods.SwRestore);
        return true;
    }

    public bool TrySetTopMost(nint hwnd, bool topMost, out int errorCode)
    {
        bool success = OperatingSystem.IsWindows() && NativeMethods.SetWindowPos(
            hwnd,
            topMost ? NativeMethods.HwndTopMost : NativeMethods.HwndNoTopMost,
            0, 0, 0, 0,
            NativeMethods.SwpNoMove | NativeMethods.SwpNoSize | NativeMethods.SwpNoActivate);
        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }

    public bool TrySetWindowRect(nint hwnd, WindowRect rect, out int errorCode)
    {
        bool success = OperatingSystem.IsWindows() && rect.IsValid && NativeMethods.SetWindowPos(
            hwnd, 0, rect.Left, rect.Top, rect.Width, rect.Height, NativeMethods.SwpNoActivate);
        errorCode = success ? 0 : Marshal.GetLastWin32Error();
        return success;
    }

    private static WindowRect ReadClientRect(nint hwnd)
    {
        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT client)) return default;
        NativeMethods.POINT origin = new() { X = client.Left, Y = client.Top };
        if (!NativeMethods.ClientToScreen(hwnd, ref origin)) return default;
        return new WindowRect(origin.X, origin.Y, client.Right - client.Left, client.Bottom - client.Top);
    }

    private static string ReadText(nint hwnd, Func<nint, int> lengthReader, Func<nint, StringBuilder, int, int> reader)
    {
        int length = lengthReader(hwnd);
        if (length <= 0) return string.Empty;
        StringBuilder text = new(length + 1);
        reader(hwnd, text, text.Capacity);
        return text.ToString();
    }

    private static string ReadProcessName(uint processId, out string? executablePath)
    {
        executablePath = null;
        try
        {
            using Process process = Process.GetProcessById(checked((int)processId));
            try { executablePath = process.MainModule?.FileName; } catch (Win32Exception) { } catch (InvalidOperationException) { }
            return !string.IsNullOrWhiteSpace(executablePath) ? Path.GetFileName(executablePath) : process.ProcessName + ".exe";
        }
        catch (ArgumentException) { return "未知进程"; }
        catch (InvalidOperationException) { return "未知进程"; }
    }

    private static uint ReadDpi(nint hwnd)
    {
        try { return NativeMethods.GetDpiForWindow(hwnd) is uint dpi and > 0 ? dpi : 96; }
        catch (EntryPointNotFoundException) { return 96; }
    }

    private static WindowRect ToRect(NativeMethods.RECT rect) => new(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
}
