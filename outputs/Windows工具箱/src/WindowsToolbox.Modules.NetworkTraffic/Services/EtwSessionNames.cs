using System.Diagnostics;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>集中定义本程序唯一可识别、可回收的 ETW 会话命名规则。</summary>
public static class EtwSessionNames
{
    public const string NetworkTraffic = "WindowsToolbox.NetworkTraffic";
    private const string LegacyPrefix = NetworkTraffic + ".";

    public static bool IsWindowsToolboxSession(string sessionName) =>
        string.Equals(sessionName, NetworkTraffic, StringComparison.Ordinal) ||
        sessionName.StartsWith(LegacyPrefix, StringComparison.Ordinal);

    public static bool IsLegacyOrphanCandidate(string sessionName, Func<int, bool> isProcessRunning)
    {
        if (!sessionName.StartsWith(LegacyPrefix, StringComparison.Ordinal))
            return false;

        string[] pieces = sessionName.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return pieces.Length == 4
            && int.TryParse(pieces[2], out int ownerProcessId)
            && ownerProcessId > 0
            && Guid.TryParseExact(pieces[3], "N", out _)
            && !isProcessRunning(ownerProcessId);
    }

    public static bool IsProcessRunning(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch (ArgumentException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
}
