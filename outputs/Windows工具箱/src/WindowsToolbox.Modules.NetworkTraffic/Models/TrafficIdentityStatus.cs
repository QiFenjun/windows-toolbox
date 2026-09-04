namespace WindowsToolbox.Modules.NetworkTraffic.Models;

/// <summary>标识流量记录是否已解析到真实进程，避免依赖显示名称猜测身份合法性。</summary>
public enum TrafficIdentityStatus
{
    Resolved = 0,
    SyntheticProcessId = 1,
    Invalid = 2
}
