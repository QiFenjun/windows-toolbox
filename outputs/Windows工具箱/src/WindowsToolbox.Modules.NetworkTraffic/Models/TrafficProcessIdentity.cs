namespace WindowsToolbox.Modules.NetworkTraffic.Models;

/// <summary>PID 与启动时间共同构成进程会话身份，避免 PID 重用串流量。</summary>
public readonly record struct TrafficProcessIdentity(int ProcessId, DateTimeOffset ProcessStartTime)
{
    public static TrafficProcessIdentity Unknown(int processId) => new(processId, DateTimeOffset.MinValue);
}
