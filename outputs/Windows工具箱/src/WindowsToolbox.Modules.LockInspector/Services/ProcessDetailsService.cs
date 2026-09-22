using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.LockInspector.Models;

namespace WindowsToolbox.Modules.LockInspector.Services;

public static class ProcessDetailsService
{
    public static bool Matches(LockingProcessInfo info, int pid, long startTime) =>
        info.ProcessId > 0 && info.ProcessStartTime > 0 && info.ProcessId == pid && info.ProcessStartTime == startTime;

    public static LockingProcessInfo Refresh(LockingProcessInfo info)
    {
        try
        {
            using Process process = Process.GetProcessById(info.ProcessId);
            if (!Matches(info, process.Id, process.StartTime.ToFileTimeUtc())) return info with { ExecutablePath = null, ProcessName = null };
            return info with { ProcessName = process.ProcessName, ExecutablePath = process.MainModule?.FileName };
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception or NotSupportedException)
        { return info with { ExecutablePath = null, ProcessName = null }; }
    }

    public static bool CanOpenLocation(LockingProcessInfo? info) => !string.IsNullOrWhiteSpace(info?.ExecutablePath) && File.Exists(info.ExecutablePath);

    public static bool OpenLocation(LockingProcessInfo info)
    {
        LockingProcessInfo current = Refresh(info); // Recheck identity immediately before the action.
        if (!CanOpenLocation(current)) return false;
        ProcessStartInfo start = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe")) { UseShellExecute = false };
        start.ArgumentList.Add("/select,"); start.ArgumentList.Add(current.ExecutablePath!);
        using Process? process = Process.Start(start);
        return process is not null;
    }

    public static void OpenTaskManager()
    {
        using Process? process = Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory, "Taskmgr.exe")) { UseShellExecute = false });
    }
}
