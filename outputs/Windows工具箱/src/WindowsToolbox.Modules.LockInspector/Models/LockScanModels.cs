namespace WindowsToolbox.Modules.LockInspector.Models;

public enum LockScanType { Files, Folder, Drive }
public enum RmApplicationType { Unknown, MainWindow, OtherWindow, Service, Explorer, Console, Critical = 1000 }

public sealed record LockingProcessInfo(
    int ProcessId, long ProcessStartTime, string ApplicationName, string ServiceShortName,
    RmApplicationType ApplicationType, uint ApplicationStatus, uint TSSessionId, bool Restartable)
{
    // FILETIME is retained without rounding: PID alone is not a process identity.
    public string Identity => ProcessId > 0 && ProcessStartTime > 0
        ? $"{ProcessId}:{ProcessStartTime}"
        : $"{ProcessId}:{ProcessStartTime}:{ServiceShortName}:{ApplicationName}";
    public string? ExecutablePath { get; init; }
    public string? ProcessName { get; init; }
    public string InfoText => $"{ApplicationName}\n{ProcessName ?? "未知 / Unknown"} · PID {ProcessId}\n" +
        $"Start FILETIME: {ProcessStartTime}\n{ApplicationType} · Status: 0x{ApplicationStatus:X} · Session: {TSSessionId}\n" +
        $"Service: {ServiceShortName}\nRestartable: {Restartable}\n{ExecutablePath ?? "路径不可用 / Path unavailable"}";
}

public sealed record LockScanRequest(IReadOnlyList<string> Targets, LockScanType ScanType = LockScanType.Files, bool Recursive = false);
public sealed record LockScanProgress(int FilesEnumerated, int FilesRegistered, int FilesSkipped, int Batches, int Blockers);
public sealed record LockScanResult
{
    public required LockScanRequest Target { get; init; }
    public DateTimeOffset StartedAt { get; init; }
    public DateTimeOffset CompletedAt { get; init; }
    public int FilesEnumerated { get; init; }
    public int FilesRegistered { get; init; }
    public int FilesSkipped { get; init; }
    public int Batches { get; init; }
    public bool WasLimited { get; init; }
    public bool WasCancelled { get; init; }
    public IReadOnlyList<int> Errors { get; init; } = [];
    public int RestartManagerError => Errors.FirstOrDefault();
    public uint RebootReason { get; init; }
    public IReadOnlyList<LockingProcessInfo> Blockers { get; init; } = [];
}

public static class RestartManagerErrors
{
    public static string Describe(int code) => code switch
    {
        0 => "完成 / Completed",
        5 => "访问被拒绝，或资源是目录；仅支持注册文件。 / Access denied or directory resource.",
        6 => "检测会话已失效，请重试。 / Invalid session; retry.",
        8 or 14 => "内存不足，无法完成检测。 / Insufficient memory.",
        29 => "当前 Restart Manager 无法检查该路径（资源登记失败；长路径可能不受支持）。 / Cannot inspect this path; registration failed, possibly an unsupported long path.",
        87 or 160 => "当前 Restart Manager 无法检查该路径或参数。 / Unsupported path or arguments.",
        121 => "Restart Manager 等待系统资源超时，请稍后重试。 / System resource timeout.",
        234 => "占用者变化过快，结果可能不完整，请重试。 / Process list changed; retry.",
        206 => "当前 Restart Manager 无法检查该长路径。 / Long path unsupported.",
        _ => "当前方法无法完成检测，请重试。 / Detection unavailable; retry."
    };
}
