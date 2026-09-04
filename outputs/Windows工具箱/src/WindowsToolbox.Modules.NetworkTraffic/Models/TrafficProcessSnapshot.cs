namespace WindowsToolbox.Modules.NetworkTraffic.Models;

public sealed record TrafficProcessSnapshot(
    TrafficProcessIdentity Identity,
    string ProcessName,
    string ExecutablePath,
    string ProductName,
    string CompanyName,
    double UploadBytesPerSecond,
    double DownloadBytesPerSecond,
    long SessionUploadBytes,
    long SessionDownloadBytes,
    IReadOnlyList<NetworkConnectionSnapshot> Connections,
    NetworkPathKind NetworkPath,
    string InterfaceName,
    bool IsVpn,
    bool IsProxy,
    bool IsLoopback,
    bool IsAttributionUncertain,
    TrafficIdentityStatus IdentityStatus);
