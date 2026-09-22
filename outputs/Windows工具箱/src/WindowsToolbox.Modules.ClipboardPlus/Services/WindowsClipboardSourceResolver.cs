using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed class WindowsClipboardSourceResolver : IClipboardSourceResolver
{
    public ClipboardSource Resolve(nint ownerWindow)
    {
        if (ownerWindow == 0)
            return new ClipboardSource(null, null);

        GetWindowThreadProcessId(ownerWindow, out uint processId);
        if (processId == 0)
            return new ClipboardSource(null, null);

        try
        {
            using Process process = Process.GetProcessById((int)processId);
            string? path = null;
            try { path = process.MainModule?.FileName; } catch { }
            return new ClipboardSource(process.ProcessName, path);
        }
        catch
        {
            return new ClipboardSource(null, null);
        }
    }

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint windowHandle, out uint processId);
}
