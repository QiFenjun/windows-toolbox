using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>在后台批量聚合 ETW 元数据；UI 只读取每秒一次的快照。</summary>
public sealed class NetworkTrafficAggregator
{
    private readonly ConcurrentDictionary<TrafficProcessIdentity, ProcessCounter> _counters = [];
    private readonly ProcessTrafficResolver _processResolver = new();
    private readonly NetworkPathResolver _pathResolver = new();
    private DateTimeOffset _lastRateSample = DateTimeOffset.UtcNow;

    public void Add(NetworkTrafficEvent trafficEvent)
    {
        // 未能归属进程的 ETW 事件不能显示为“PID -1”应用，也不能计入任意应用总量。
        if (trafficEvent.SizeBytes <= 0 || trafficEvent.ProcessId <= 0)
            return;

        TrafficProcessIdentity identity = ResolveIdentity(trafficEvent.ProcessId);
        ProcessCounter counter = _counters.GetOrAdd(identity, static key => new ProcessCounter(key));
        counter.Add(trafficEvent);
    }

    public IReadOnlyList<ApplicationTrafficGroup> Snapshot(
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        IReadOnlyList<NetworkInterfaceSnapshot> interfaces,
        string sortMode,
        string filter,
        string searchText)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        double elapsed = Math.Max(0.2, (now - _lastRateSample).TotalSeconds);
        _lastRateSample = now;
        Dictionary<int, string> processNames = ResolveProcessNames();
        List<TrafficProcessSnapshot> processes = [];

        foreach ((TrafficProcessIdentity identity, ProcessCounter counter) in _counters)
        {
            IReadOnlyList<NetworkConnectionSnapshot> processConnections = connections
                .Where(connection => connection.ProcessId == identity.ProcessId)
                .ToArray();
            NetworkTrafficEvent? last = counter.LastEvent;
            NetworkPathResolution path = last is null
                ? new(NetworkPathKind.Unknown, IsAttributionUncertain: true)
                : _pathResolver.Resolve(last, connections, processNames, interfaces, processConnections);
            (double uploadRate, double downloadRate) = counter.TakeRates(elapsed);
            processes.Add(_processResolver.Resolve(
                identity,
                counter.SessionUploadBytes,
                counter.SessionDownloadBytes,
                uploadRate,
                downloadRate,
                processConnections,
                path));
        }

        IEnumerable<ApplicationTrafficGroup> groups = processes
            .GroupBy(GetApplicationKey, StringComparer.OrdinalIgnoreCase)
            .Select(CreateGroup);

        if (!string.IsNullOrWhiteSpace(searchText))
        {
            groups = groups.Where(group => group.DisplayName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
                                           group.CompanyName.Contains(searchText, StringComparison.CurrentCultureIgnoreCase));
        }

        groups = filter switch
        {
            "活动中" => groups.Where(group => group.DownloadBytesPerSecond + group.UploadBytesPerSecond > 0),
            "直连" => groups.Where(group => group.NetworkPath == NetworkPathKind.Direct),
            "VPN" => groups.Where(group => group.NetworkPath == NetworkPathKind.Vpn),
            "代理" => groups.Where(group => group.NetworkPath == NetworkPathKind.LocalProxy),
            "系统" => groups.Where(group => group.DisplayName.StartsWith("System", StringComparison.OrdinalIgnoreCase) || group.DisplayName.StartsWith("PID", StringComparison.OrdinalIgnoreCase)),
            _ => groups
        };

        return sortMode switch
        {
            "名称" => groups.OrderBy(group => group.DisplayName, StringComparer.CurrentCultureIgnoreCase).ToArray(),
            "上传速度" => groups.OrderByDescending(group => group.UploadBytesPerSecond).ToArray(),
            "总流量" => groups.OrderByDescending(group => group.TotalSessionBytes).ToArray(),
            "连接数" => groups.OrderByDescending(group => group.ConnectionCount).ToArray(),
            _ => groups.OrderByDescending(group => group.DownloadBytesPerSecond).ToArray()
        };
    }

    private static TrafficProcessIdentity ResolveIdentity(int processId)
    {
        try
        {
            using Process process = Process.GetProcessById(processId);
            return new(processId, process.StartTime.ToUniversalTime());
        }
        catch (ArgumentException) { return TrafficProcessIdentity.Unknown(processId); }
        catch (InvalidOperationException) { return TrafficProcessIdentity.Unknown(processId); }
        catch (Win32Exception) { return TrafficProcessIdentity.Unknown(processId); }
    }

    private static Dictionary<int, string> ResolveProcessNames()
    {
        Dictionary<int, string> values = [];
        foreach (Process process in Process.GetProcesses())
        {
            try { values[process.Id] = process.ProcessName; }
            catch (InvalidOperationException) { }
            finally { process.Dispose(); }
        }
        return values;
    }

    private static string GetApplicationKey(TrafficProcessSnapshot process)
    {
        if (!string.IsNullOrWhiteSpace(process.ExecutablePath))
            return process.ExecutablePath;
        return $"{process.CompanyName}|{process.ProductName}|{process.ProcessName}";
    }

    private static ApplicationTrafficGroup CreateGroup(IGrouping<string, TrafficProcessSnapshot> grouping)
    {
        TrafficProcessSnapshot first = grouping.First();
        TrafficProcessSnapshot[] processes = grouping.ToArray();
        NetworkPathKind path = processes.Any(item => item.NetworkPath == NetworkPathKind.LocalProxy)
            ? NetworkPathKind.LocalProxy
            : processes.Any(item => item.NetworkPath == NetworkPathKind.Vpn)
                ? NetworkPathKind.Vpn
                : processes.All(item => item.NetworkPath == NetworkPathKind.Loopback)
                    ? NetworkPathKind.Loopback
                    : processes.Any(item => item.NetworkPath == NetworkPathKind.Direct)
                        ? NetworkPathKind.Direct
                        : NetworkPathKind.Unknown;
        string name = !string.IsNullOrWhiteSpace(first.ProductName) ? first.ProductName : first.ProcessName;
        return new ApplicationTrafficGroup
        {
            Id = Hash(grouping.Key),
            DisplayName = name,
            ExecutablePath = first.ExecutablePath,
            CompanyName = first.CompanyName,
            DownloadBytesPerSecond = processes.Sum(item => item.DownloadBytesPerSecond),
            UploadBytesPerSecond = processes.Sum(item => item.UploadBytesPerSecond),
            SessionDownloadBytes = processes.Sum(item => item.SessionDownloadBytes),
            SessionUploadBytes = processes.Sum(item => item.SessionUploadBytes),
            TcpConnectionCount = processes.Sum(item => item.Connections.Count(connection => connection.Protocol == "TCP")),
            UdpConnectionCount = processes.Sum(item => item.Connections.Count(connection => connection.Protocol == "UDP")),
            NetworkPath = path,
            InterfaceName = processes.FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.InterfaceName))?.InterfaceName ?? "未知",
            ProxyProcessName = processes.Select(item => item.NetworkPath).Any(item => item == NetworkPathKind.LocalProxy) ? "本地代理" : string.Empty,
            IsAttributionUncertain = processes.Any(item => item.IsAttributionUncertain),
            IdentityStatus = processes.Any(item => item.IdentityStatus == TrafficIdentityStatus.Resolved)
                ? TrafficIdentityStatus.Resolved
                : TrafficIdentityStatus.SyntheticProcessId,
            Processes = processes
        };
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)))[..16];

    private sealed class ProcessCounter(TrafficProcessIdentity identity)
    {
        private long _intervalUpload;
        private long _intervalDownload;
        public TrafficProcessIdentity Identity { get; } = identity;
        public long SessionUploadBytes { get; private set; }
        public long SessionDownloadBytes { get; private set; }
        public NetworkTrafficEvent? LastEvent { get; private set; }

        public void Add(NetworkTrafficEvent trafficEvent)
        {
            LastEvent = trafficEvent;
            if (trafficEvent.Direction == TrafficDirection.Upload)
            {
                SessionUploadBytes += trafficEvent.SizeBytes;
                Interlocked.Add(ref _intervalUpload, trafficEvent.SizeBytes);
            }
            else
            {
                SessionDownloadBytes += trafficEvent.SizeBytes;
                Interlocked.Add(ref _intervalDownload, trafficEvent.SizeBytes);
            }
        }

        public (double Upload, double Download) TakeRates(double elapsed) =>
            (Interlocked.Exchange(ref _intervalUpload, 0) / elapsed,
             Interlocked.Exchange(ref _intervalDownload, 0) / elapsed);
    }
}
