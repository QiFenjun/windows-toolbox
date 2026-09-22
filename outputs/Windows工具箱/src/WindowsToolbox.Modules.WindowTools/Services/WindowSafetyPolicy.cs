using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

/// <summary>仅过滤明确的系统 Shell 窗口；普通 Explorer 窗口仍可操作。</summary>
public sealed class WindowSafetyPolicy
{
    private static readonly HashSet<string> SystemClasses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Progman", "WorkerW", "Shell_TrayWnd", "Shell_SecondaryTrayWnd", "LockScreenBackstopFrame"
    };

    public bool IsListable(WindowNativeSnapshot window) =>
        window.Hwnd != 0 && window.IsVisible && !string.IsNullOrWhiteSpace(window.Title) &&
        !IsSystemShellWindow(window) && !IsToolboxHelper(window);

    public bool CanModify(WindowNativeSnapshot window) =>
        window.Hwnd != 0 && !IsSystemShellWindow(window) && !IsToolboxHelper(window) &&
        window.ProcessId != (uint)Environment.ProcessId;

    public bool IsSystemShellWindow(WindowNativeSnapshot window) => SystemClasses.Contains(window.ClassName);

    private static bool IsToolboxHelper(WindowNativeSnapshot window) =>
        window.ClassName.StartsWith("WindowsToolbox.", StringComparison.OrdinalIgnoreCase) ||
        (!string.IsNullOrWhiteSpace(window.ExecutablePath) && window.Title.Length == 0);
}
