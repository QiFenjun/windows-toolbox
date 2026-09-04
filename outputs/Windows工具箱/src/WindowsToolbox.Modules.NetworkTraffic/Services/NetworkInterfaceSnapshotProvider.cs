using System.Net.NetworkInformation;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>接口实际字节独立于应用 ETW 统计，绝不与应用流量相加。</summary>
public sealed class NetworkInterfaceSnapshotProvider
{
    private readonly Dictionary<string, (long InBytes, long OutBytes, DateTimeOffset Timestamp)> _previous = [];

    public IReadOnlyList<NetworkInterfaceSnapshot> Read()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        List<NetworkInterfaceSnapshot> snapshots = [];
        foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
        {
            try
            {
                IPv4InterfaceStatistics stats = networkInterface.GetIPv4Statistics();
                int interfaceIndex = networkInterface.GetIPProperties().GetIPv4Properties().Index;
                long received = stats.BytesReceived;
                long sent = stats.BytesSent;
                double down = 0;
                double up = 0;
                if (_previous.TryGetValue(networkInterface.Id, out var previous))
                {
                    double seconds = Math.Max(0.001, (now - previous.Timestamp).TotalSeconds);
                    down = Math.Max(0, received - previous.InBytes) / seconds;
                    up = Math.Max(0, sent - previous.OutBytes) / seconds;
                }
                _previous[networkInterface.Id] = (received, sent, now);
                snapshots.Add(new NetworkInterfaceSnapshot(
                    networkInterface.Id,
                    interfaceIndex,
                    networkInterface.Name,
                    networkInterface.Description,
                    GetTypeText(networkInterface.NetworkInterfaceType),
                    Classify(networkInterface),
                    networkInterface.OperationalStatus == OperationalStatus.Up,
                    received,
                    sent,
                    down,
                    up));
            }
            catch (NetworkInformationException)
            {
                // 接口切换期间单项失败不影响其他接口。
            }
        }
        return snapshots.OrderByDescending(item => item.IsOperational).ThenBy(item => item.Name).ToArray();
    }

    public static NetworkPathKind Classify(NetworkInterface networkInterface)
    {
        return Classify(networkInterface.NetworkInterfaceType, networkInterface.Name, networkInterface.Description);
    }

    public static NetworkPathKind Classify(NetworkInterfaceType type, string name, string description)
    {
        if (type == NetworkInterfaceType.Loopback)
            return NetworkPathKind.Loopback;
        if (type is NetworkInterfaceType.Ppp or NetworkInterfaceType.Tunnel)
            return NetworkPathKind.Vpn;

        string evidence = $"{name} {description}";
        return evidence.Contains("wintun", StringComparison.OrdinalIgnoreCase) ||
               evidence.Contains("wireguard", StringComparison.OrdinalIgnoreCase) ||
               evidence.Contains("tap", StringComparison.OrdinalIgnoreCase) ||
               evidence.Contains("tun", StringComparison.OrdinalIgnoreCase) ||
               evidence.Contains("vpn", StringComparison.OrdinalIgnoreCase)
            ? NetworkPathKind.Vpn
            : NetworkPathKind.Direct;
    }

    private static string GetTypeText(NetworkInterfaceType type) => type switch
    {
        NetworkInterfaceType.Wireless80211 => "Wi-Fi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet or NetworkInterfaceType.FastEthernetFx => "以太网",
        NetworkInterfaceType.Ppp => "PPP",
        NetworkInterfaceType.Tunnel => "Tunnel",
        NetworkInterfaceType.Loopback => "Loopback",
        _ => type.ToString()
    };
}
