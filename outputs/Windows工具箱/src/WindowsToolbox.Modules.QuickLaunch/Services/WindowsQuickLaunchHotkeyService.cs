using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public sealed class WindowsQuickLaunchHotkeyService : IQuickLaunchHotkeyService
{
    private const int WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModWin = 0x0008;
    private const int HotkeyId = 0x5751;
    private HwndSource? _window;

    public bool IsRegistered { get; private set; }
    public event EventHandler? Pressed;

    public bool Start(bool enabled = true)
    {
        if (!enabled)
        {
            Stop();
            return false;
        }
        if (_window is not null) return IsRegistered;
        if (!OperatingSystem.IsWindows()) return false;

        HwndSourceParameters parameters = new("WindowsToolbox.QuickLaunch.Hotkey")
        {
            Width = 1,
            Height = 1,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x00000080
        };
        _window = new HwndSource(parameters);
        _window.AddHook(WndProc);
        IsRegistered = RegisterHotKey(_window.Handle, HotkeyId, ModWin | ModAlt, 0x51);
        if (!IsRegistered) Stop();
        return IsRegistered;
    }

    public void Stop()
    {
        if (_window is null) return;
        if (IsRegistered) UnregisterHotKey(_window.Handle, HotkeyId);
        IsRegistered = false;
        _window.RemoveHook(WndProc);
        _window.Dispose();
        _window = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            Pressed?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
