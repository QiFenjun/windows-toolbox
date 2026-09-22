using System.Runtime.InteropServices;
using System.Text;
using WindowsToolbox.Modules.LockInspector.Models;

namespace WindowsToolbox.Modules.LockInspector.Interop;

[StructLayout(LayoutKind.Sequential)]
public struct RmUniqueProcess
{
    public int ProcessId;
    public System.Runtime.InteropServices.ComTypes.FILETIME StartTime;
    public readonly long FileTime => ((long)(uint)StartTime.dwHighDateTime << 32) | (uint)StartTime.dwLowDateTime;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct RmProcessInfo
{
    public RmUniqueProcess Process;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string ApplicationName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string ServiceShortName;
    public RmApplicationType ApplicationType;
    public uint ApplicationStatus;
    public uint TSSessionId;
    [MarshalAs(UnmanagedType.Bool)] public bool Restartable;

    public readonly LockingProcessInfo ToModel() => new(Process.ProcessId, Process.FileTime,
        ApplicationName ?? "", ServiceShortName ?? "", ApplicationType, ApplicationStatus, TSSessionId, Restartable);
}

internal static class RestartManagerNative
{
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RmStartSession(out uint session, uint flags, StringBuilder sessionKey);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RmRegisterResources(uint session, uint fileCount,
        [In, MarshalAs(UnmanagedType.LPArray, ArraySubType = UnmanagedType.LPWStr)] string[] files,
        uint applicationCount, IntPtr applications, uint serviceCount, IntPtr services);
    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    internal static extern int RmGetList(uint session, out uint needed, ref uint count,
        [In, Out] RmProcessInfo[]? processes, out uint rebootReasons);
    [DllImport("rstrtmgr.dll", ExactSpelling = true)]
    internal static extern int RmEndSession(uint session);
}
