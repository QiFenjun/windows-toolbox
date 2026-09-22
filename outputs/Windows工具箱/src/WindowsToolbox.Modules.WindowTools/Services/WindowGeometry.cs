using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

/// <summary>仅按显示器工作区计算一次性布局，不干预 Windows Snap。</summary>
public static class WindowGeometry
{
    public static WindowRect Center(WindowRect window, WindowRect workArea)
    {
        WindowRect fitted = Fit(window, workArea);
        return new WindowRect(
            workArea.Left + (workArea.Width - fitted.Width) / 2,
            workArea.Top + (workArea.Height - fitted.Height) / 2,
            fitted.Width,
            fitted.Height);
    }

    public static WindowRect Resize(WindowRect current, int width, int height, WindowRect workArea) =>
        Fit(new WindowRect(current.Left, current.Top, width, height), workArea);

    public static WindowRect Layout(WindowLayoutPreset preset, WindowRect workArea, WindowRect current) => preset switch
    {
        WindowLayoutPreset.LeftHalf => new WindowRect(workArea.Left, workArea.Top, workArea.Width / 2, workArea.Height),
        WindowLayoutPreset.RightHalf => new WindowRect(workArea.Left + workArea.Width / 2, workArea.Top, workArea.Width - workArea.Width / 2, workArea.Height),
        WindowLayoutPreset.TopLeft => new WindowRect(workArea.Left, workArea.Top, workArea.Width / 2, workArea.Height / 2),
        WindowLayoutPreset.TopRight => new WindowRect(workArea.Left + workArea.Width / 2, workArea.Top, workArea.Width - workArea.Width / 2, workArea.Height / 2),
        WindowLayoutPreset.BottomLeft => new WindowRect(workArea.Left, workArea.Top + workArea.Height / 2, workArea.Width / 2, workArea.Height - workArea.Height / 2),
        WindowLayoutPreset.BottomRight => new WindowRect(workArea.Left + workArea.Width / 2, workArea.Top + workArea.Height / 2, workArea.Width - workArea.Width / 2, workArea.Height - workArea.Height / 2),
        _ => Center(current, workArea)
    };

    public static WindowRect Fit(WindowRect window, WindowRect workArea)
    {
        int width = Math.Clamp(window.Width, 1, Math.Max(1, workArea.Width));
        int height = Math.Clamp(window.Height, 1, Math.Max(1, workArea.Height));
        int left = Math.Clamp(window.Left, workArea.Left, workArea.Right - width);
        int top = Math.Clamp(window.Top, workArea.Top, workArea.Bottom - height);
        return new WindowRect(left, top, width, height);
    }
}
