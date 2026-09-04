using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Modules.NetworkTraffic.Models;

/// <summary>同一软件的多个进程在 UI 中聚合显示，保留可展开的进程快照。</summary>
public sealed class ApplicationTrafficGroup : ObservableObject
{
    private bool _isExpanded;

    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ExecutablePath { get; init; } = string.Empty;
    public string CompanyName { get; init; } = string.Empty;
    public double UploadBytesPerSecond { get; init; }
    public double DownloadBytesPerSecond { get; init; }
    public long SessionUploadBytes { get; init; }
    public long SessionDownloadBytes { get; init; }
    public long TodayUploadBytes { get; set; }
    public long TodayDownloadBytes { get; set; }
    public int TcpConnectionCount { get; init; }
    public int UdpConnectionCount { get; init; }
    public NetworkPathKind NetworkPath { get; init; }
    public string InterfaceName { get; init; } = "未知";
    public string ProxyProcessName { get; init; } = string.Empty;
    public bool IsAttributionUncertain { get; init; }
    public TrafficIdentityStatus IdentityStatus { get; init; }
    public IReadOnlyList<TrafficProcessSnapshot> Processes { get; init; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set => SetProperty(ref _isExpanded, value);
    }

    public int ProcessCount => Processes.Count;
    public int ConnectionCount => TcpConnectionCount + UdpConnectionCount;
    public long TotalSessionBytes => SessionUploadBytes + SessionDownloadBytes;
    public long TotalTodayBytes => TodayUploadBytes + TodayDownloadBytes;
    public string DownloadRateText => TrafficDisplayFormatter.Rate(DownloadBytesPerSecond);
    public string UploadRateText => TrafficDisplayFormatter.Rate(UploadBytesPerSecond);
    public string SessionTotalText => TrafficDisplayFormatter.Bytes(TotalSessionBytes);
    public string TodayTotalText => TrafficDisplayFormatter.Bytes(TotalTodayBytes);
    public string PathText => NetworkPath switch
    {
        NetworkPathKind.Vpn => "VPN / Tunnel",
        NetworkPathKind.LocalProxy => "本地代理",
        NetworkPathKind.Loopback => "Loopback",
        NetworkPathKind.Direct => "直连",
        _ => "未知"
    };
}
