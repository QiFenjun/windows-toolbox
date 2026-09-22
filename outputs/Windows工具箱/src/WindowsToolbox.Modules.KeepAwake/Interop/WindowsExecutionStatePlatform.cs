using System.Runtime.InteropServices;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;

namespace WindowsToolbox.Modules.KeepAwake.Interop;

public sealed class WindowsExecutionStatePlatform : IExecutionStatePlatform
{
    public ExecutionStateResult Set(ExecutionState flags)
    {
        uint previous = SetThreadExecutionState(flags);
        // This API does not promise extended error information. Zero means unavailable, not success.
        return new(previous, previous == 0 ? Marshal.GetLastWin32Error() : 0);
    }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint SetThreadExecutionState(ExecutionState flags);
}
