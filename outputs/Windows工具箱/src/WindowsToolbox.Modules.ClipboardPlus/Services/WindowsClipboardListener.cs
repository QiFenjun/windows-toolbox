using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed class WindowsClipboardListener : IClipboardListener
{
    private const int WmClipboardUpdate = 0x031D;
    private const int WmHotKey = 0x0312;
    private const int ModAlt = 0x0001;
    private const int ModWin = 0x0008;
    private const int HotkeyId = 0x5742;
    private HwndSource? _window;
    private IClipboardAdapter? _adapter;
    private bool _hotkeyRegistered;

    public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    public event EventHandler? HotkeyPressed;
    public bool IsRunning => _window is not null;
    public bool IsHotkeyRegistered => _hotkeyRegistered;

    public bool Start(bool registerHotkey = true)
    {
        if (_window is not null)
            return true;
        if (!OperatingSystem.IsWindows())
            return false;

        HwndSourceParameters parameters = new("WindowsToolbox.ClipboardPlus.Listener")
        {
            Width = 1,
            Height = 1,
            WindowStyle = unchecked((int)0x80000000),
            ExtendedWindowStyle = 0x00000080
        };
        _window = new HwndSource(parameters);
        _window.AddHook(WndProc);
        _adapter = new WindowsClipboardAdapter();

        if (!AddClipboardFormatListener(_window.Handle))
        {
            Stop();
            return false;
        }

        _hotkeyRegistered = registerHotkey && RegisterHotKey(_window.Handle, HotkeyId, ModWin | ModAlt, 0x56);
        return true;
    }

    public void Stop()
    {
        if (_window is null)
            return;

        RemoveClipboardFormatListener(_window.Handle);
        if (_hotkeyRegistered)
            UnregisterHotKey(_window.Handle, HotkeyId);
        _hotkeyRegistered = false;
        _window.RemoveHook(WndProc);
        _window.Dispose();
        _window = null;
        _adapter = null;
    }

    private IntPtr WndProc(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message == WmClipboardUpdate)
        {
            handled = true;
            _ = ReadClipboardAsync();
        }
        else if (message == WmHotKey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }
        return IntPtr.Zero;
    }

    private async Task ReadClipboardAsync()
    {
        await Task.Yield();
        if (_adapter is null)
            return;

        foreach (int delay in new[] { 10, 30, 80 })
        {
            try
            {
                if (_adapter.ContainsUnicodeText())
                {
                    string? text = _adapter.GetUnicodeText();
                    if (ClipboardOptions.IsValidText(text))
                    {
                        ClipboardChanged?.Invoke(this,
                            new ClipboardChangedEventArgs(text!, _adapter.GetSequenceNumber(), _adapter.GetOwnerWindow()));
                        return;
                    }
                }
            }
            catch (COMException) { }
            catch (ExternalException) { }

            await Task.Delay(delay).ConfigureAwait(true);
        }
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint virtualKey);
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(nint hwnd, int id);
}
