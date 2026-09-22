using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

/// <summary>可替换的最小 Win32 边界，测试不会触碰用户真实窗口。</summary>
public interface IWindowPlatform
{
    IReadOnlyList<WindowNativeSnapshot> EnumerateTopLevelWindows();
    bool TryGetWindow(nint hwnd, out WindowNativeSnapshot snapshot);
    IReadOnlyList<MonitorSnapshot> EnumerateMonitors();
    nint GetForegroundWindow();
    bool TryRestore(nint hwnd, out int errorCode);
    bool TrySetTopMost(nint hwnd, bool topMost, out int errorCode);
    bool TrySetWindowRect(nint hwnd, WindowRect rect, out int errorCode);
}

public interface IWindowEnumerator
{
    IReadOnlyList<WindowSnapshot> Enumerate();
    WindowSnapshot? TryGetWindow(nint hwnd);
    nint GetForegroundWindow();
}

public interface IMonitorService
{
    IReadOnlyList<MonitorSnapshot> GetMonitors();
}

public interface IWindowController
{
    WindowOperationResult SetTopMost(WindowSnapshot window, bool topMost);
    WindowOperationResult Center(WindowSnapshot window);
    WindowOperationResult Resize(WindowSnapshot window, int width, int height);
    WindowOperationResult ApplyLayout(WindowSnapshot window, WindowLayoutPreset preset);
    WindowOperationResult MoveToMonitor(WindowSnapshot window, string monitorId);
}
