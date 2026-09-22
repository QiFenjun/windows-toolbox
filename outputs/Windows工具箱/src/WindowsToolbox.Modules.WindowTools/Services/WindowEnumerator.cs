using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

public sealed class WindowEnumerator(IWindowPlatform platform, WindowSafetyPolicy safetyPolicy) : IWindowEnumerator
{
    public IReadOnlyList<WindowSnapshot> Enumerate()
    {
        IReadOnlyList<MonitorSnapshot> monitors = platform.EnumerateMonitors();
        return platform.EnumerateTopLevelWindows()
            .Where(safetyPolicy.IsListable)
            .Select(window => CreateSnapshot(window, monitors))
            .OrderBy(window => window.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    public WindowSnapshot? TryGetWindow(nint hwnd)
    {
        if (!platform.TryGetWindow(hwnd, out WindowNativeSnapshot window) || !safetyPolicy.IsListable(window)) return null;
        return CreateSnapshot(window, platform.EnumerateMonitors());
    }

    public nint GetForegroundWindow() => platform.GetForegroundWindow();

    private WindowSnapshot CreateSnapshot(WindowNativeSnapshot window, IReadOnlyList<MonitorSnapshot> monitors)
    {
        MonitorSnapshot? monitor = monitors.FirstOrDefault(item => item.Handle == window.MonitorHandle) ?? monitors.FirstOrDefault(item => item.IsPrimary);
        return new WindowSnapshot(
            window.Hwnd, window.Title, window.ProcessId, window.ProcessName, window.ExecutablePath, window.ClassName,
            window.WindowRect, window.ClientRect, window.IsVisible, window.State, window.IsTopMost, window.MonitorHandle,
            monitor?.Id ?? "", monitor?.DisplayName ?? "未知显示器", monitor?.WorkingArea ?? default,
            window.Dpi, safetyPolicy.CanModify(window), DateTimeOffset.Now);
    }
}
