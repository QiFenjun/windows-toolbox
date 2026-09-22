using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

public sealed class WindowController(IWindowPlatform platform, IMonitorService monitors, WindowSafetyPolicy safetyPolicy) : IWindowController
{
    public WindowOperationResult SetTopMost(WindowSnapshot window, bool topMost)
    {
        if (!TryValidate(window, out _, out WindowOperationResult? validationFailure)) return validationFailure!;
        return platform.TrySetTopMost(window.Hwnd, topMost, out int error)
            ? WindowOperationResult.Ok(topMost ? "已置顶。" : "已取消置顶。")
            : Failure(error);
    }

    public WindowOperationResult Center(WindowSnapshot window) => ApplyRect(window, current => WindowGeometry.Center(current.WindowRect, MonitorFor(current).WorkingArea), "已居中。");

    public WindowOperationResult Resize(WindowSnapshot window, int width, int height)
    {
        if (width <= 0) return new(false, 0, "宽度必须大于 0。");
        if (height <= 0) return new(false, 0, "高度必须大于 0。");
        return ApplyRect(window, current => WindowGeometry.Resize(current.WindowRect, width, height, MonitorFor(current).WorkingArea), "已调整窗口外框尺寸。");
    }

    public WindowOperationResult ApplyLayout(WindowSnapshot window, WindowLayoutPreset preset) =>
        ApplyRect(window, current => WindowGeometry.Layout(preset, MonitorFor(current).WorkingArea, current.WindowRect), "已应用窗口布局。");

    public WindowOperationResult MoveToMonitor(WindowSnapshot window, string monitorId)
    {
        if (!TryValidate(window, out WindowNativeSnapshot current, out WindowOperationResult? validationFailure)) return validationFailure!;
        if (!RestoreIfNeeded(window.Hwnd, current, out current, out WindowOperationResult? restoreFailure)) return restoreFailure!;
        MonitorSnapshot? destination = monitors.GetMonitors().FirstOrDefault(item => string.Equals(item.Id, monitorId, StringComparison.OrdinalIgnoreCase));
        if (destination is null) return new(false, 0, "目标显示器不可用。");
        WindowRect rect = WindowGeometry.Center(current.WindowRect, destination.WorkingArea);
        return platform.TrySetWindowRect(window.Hwnd, rect, out int error)
            ? WindowOperationResult.Ok($"已移动到 {destination.DisplayName}。")
            : Failure(error);
    }

    private WindowOperationResult ApplyRect(WindowSnapshot window, Func<WindowNativeSnapshot, WindowRect> createRect, string successMessage)
    {
        if (!TryValidate(window, out WindowNativeSnapshot current, out WindowOperationResult? validationFailure)) return validationFailure!;
        if (!RestoreIfNeeded(window.Hwnd, current, out current, out WindowOperationResult? restoreFailure)) return restoreFailure!;
        return platform.TrySetWindowRect(window.Hwnd, createRect(current), out int error)
            ? WindowOperationResult.Ok(successMessage)
            : Failure(error);
    }

    private bool TryValidate(WindowSnapshot expected, out WindowNativeSnapshot current, out WindowOperationResult? failure)
    {
        current = default!;
        failure = null;
        if (!platform.TryGetWindow(expected.Hwnd, out current))
        {
            failure = new(false, 1400, "窗口已关闭。");
            return false;
        }
        if (current.ProcessId != expected.ProcessId)
        {
            failure = new(false, 1400, "窗口已变更，无法继续操作。");
            return false;
        }
        if (!safetyPolicy.CanModify(current))
        {
            failure = new(false, 5, "该窗口受系统保护或属于 Windows 工具箱，无法修改。");
            return false;
        }
        return true;
    }

    private bool RestoreIfNeeded(nint hwnd, WindowNativeSnapshot before, out WindowNativeSnapshot current, out WindowOperationResult? failure)
    {
        current = before;
        failure = null;
        if (before.State == WindowStateKind.Normal) return true;
        if (!platform.TryRestore(hwnd, out int error))
        {
            failure = Failure(error);
            return false;
        }
        if (!platform.TryGetWindow(hwnd, out current))
        {
            failure = new(false, 1400, "窗口已关闭。");
            return false;
        }
        return true;
    }

    private MonitorSnapshot MonitorFor(WindowNativeSnapshot window) =>
        monitors.GetMonitors().FirstOrDefault(item => item.Handle == window.MonitorHandle) ??
        monitors.GetMonitors().FirstOrDefault(item => item.IsPrimary) ??
        new MonitorSnapshot(0, "", "当前显示器", window.WindowRect, window.WindowRect, true, window.Dpi);

    private static WindowOperationResult Failure(int errorCode) => errorCode switch
    {
        5 => new(false, errorCode, "目标窗口权限高于 Windows 工具箱，当前无法执行该操作。"),
        1400 => new(false, errorCode, "窗口已关闭。"),
        _ => new(false, errorCode, "无法执行窗口操作。")
    };
}
