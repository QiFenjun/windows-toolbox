namespace WindowsToolbox.Modules.WindowTools.Models;

/// <summary>Win32 窗口外框物理像素矩形。</summary>
public readonly record struct WindowRect(int Left, int Top, int Width, int Height)
{
    public int Right => Left + Width;
    public int Bottom => Top + Height;
    public bool IsValid => Width > 0 && Height > 0;
    public override string ToString() => $"{Left},{Top} {Width}×{Height}";
}

public enum WindowStateKind { Normal, Minimized, Maximized }

public enum WindowLayoutPreset { LeftHalf, RightHalf, TopLeft, TopRight, BottomLeft, BottomRight, Center }

public sealed record MonitorSnapshot(
    nint Handle,
    string Id,
    string DisplayName,
    WindowRect Bounds,
    WindowRect WorkingArea,
    bool IsPrimary,
    uint Dpi)
{
    public string DisplayText => $"{DisplayName} · {Bounds.Width}×{Bounds.Height}" + (IsPrimary ? " · Primary" : string.Empty);
}

/// <summary>平台读取的原始窗口元数据，不含 UI 状态。</summary>
public sealed record WindowNativeSnapshot(
    nint Hwnd,
    string Title,
    uint ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string ClassName,
    WindowRect WindowRect,
    WindowRect ClientRect,
    bool IsVisible,
    WindowStateKind State,
    bool IsTopMost,
    nint MonitorHandle,
    uint Dpi);

public sealed record WindowSnapshot(
    nint Hwnd,
    string Title,
    uint ProcessId,
    string ProcessName,
    string? ExecutablePath,
    string ClassName,
    WindowRect WindowRect,
    WindowRect ClientRect,
    bool IsVisible,
    WindowStateKind State,
    bool IsTopMost,
    nint MonitorHandle,
    string MonitorId,
    string MonitorName,
    WindowRect MonitorWorkingArea,
    uint Dpi,
    bool IsModifiable,
    DateTimeOffset CapturedAt)
{
    public string HwndText => $"0x{Hwnd.ToInt64():X16}";
    public string StateText => State switch
    {
        WindowStateKind.Minimized => "最小化",
        WindowStateKind.Maximized => "最大化",
        _ => IsTopMost ? "置顶" : "普通"
    };
}

public sealed record WindowOperationResult(bool Success, int ErrorCode, string UserMessage)
{
    public static WindowOperationResult Ok(string message) => new(true, 0, message);
}

public sealed record WindowLayoutOption(WindowLayoutPreset Value, string DisplayName);
