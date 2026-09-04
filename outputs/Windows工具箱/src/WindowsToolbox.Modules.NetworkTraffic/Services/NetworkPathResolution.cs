using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

public sealed record NetworkPathResolution(
    NetworkPathKind Kind,
    string InterfaceName = "未知",
    string ProxyProcessName = "",
    bool IsAttributionUncertain = false,
    ProxyDetectionConfidence ProxyConfidence = ProxyDetectionConfidence.None);

public enum ProxyDetectionConfidence
{
    None,
    Medium,
    High
}
