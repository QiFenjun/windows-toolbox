using System.Net;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>基于连接快照判断 Loopback 和本地代理；未知情形不做猜测。</summary>
public sealed class NetworkPathResolver
{
    private readonly IInterfaceRouteResolver _routeResolver;
    private readonly ISystemProxyConfigurationProvider _systemProxyProvider;

    public NetworkPathResolver(
        IInterfaceRouteResolver? routeResolver = null,
        ISystemProxyConfigurationProvider? systemProxyProvider = null)
    {
        _routeResolver = routeResolver ?? new IpHelperRouteResolver();
        _systemProxyProvider = systemProxyProvider ?? new SystemProxyConfigurationProvider();
    }

    public NetworkPathResolution Resolve(
        NetworkTrafficEvent trafficEvent,
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        IReadOnlyDictionary<int, string> processNames,
        IReadOnlyList<NetworkInterfaceSnapshot>? interfaces = null,
        IReadOnlyList<NetworkConnectionSnapshot>? processConnections = null)
    {
        // ETW 的最后一个事件可能已经是代理进程发出的外网包。优先使用当前进程的
        // 连接快照，避免浏览器仍连着 127.0.0.1 代理时被误显示为“直连”。
        NetworkPathResolution? proxyPath = ResolveLocalProxyFromProcessConnections(
            trafficEvent.ProcessId,
            processConnections,
            connections,
            processNames);
        if (proxyPath is not null)
            return proxyPath;

        string remote = trafficEvent.Direction == TrafficDirection.Upload
            ? trafficEvent.DestinationAddress : trafficEvent.SourceAddress;
        int remotePort = trafficEvent.Direction == TrafficDirection.Upload
            ? trafficEvent.DestinationPort : trafficEvent.SourcePort;

        if (!IPAddress.TryParse(remote, out IPAddress? remoteAddress))
            return new(NetworkPathKind.Unknown, IsAttributionUncertain: true);

        if (!IPAddress.IsLoopback(remoteAddress))
        {
            if (interfaces is not null && _routeResolver.TryResolve(remote, interfaces, out NetworkInterfaceSnapshot networkInterface))
                return new(networkInterface.PathKind, networkInterface.Name);
            return new(NetworkPathKind.Unknown, IsAttributionUncertain: true);
        }

        NetworkConnectionSnapshot? listener = FindListener(
            connections,
            trafficEvent.ProcessId,
            trafficEvent.Protocol,
            remotePort);

        if (listener is not null)
            return TryCreateLocalProxyResolution(listener, connections, processNames)
                ?? new(NetworkPathKind.Loopback, "Loopback");

        return new(NetworkPathKind.Loopback, "Loopback");
    }

    private NetworkPathResolution? ResolveLocalProxyFromProcessConnections(
        int processId,
        IReadOnlyList<NetworkConnectionSnapshot>? processConnections,
        IReadOnlyList<NetworkConnectionSnapshot> allConnections,
        IReadOnlyDictionary<int, string> processNames)
    {
        if (processConnections is null)
            return null;

        foreach (NetworkConnectionSnapshot connection in processConnections)
        {
            if (connection.ProcessId != processId ||
                !IPAddress.TryParse(connection.RemoteAddress, out IPAddress? remoteAddress) ||
                !IPAddress.IsLoopback(remoteAddress))
            {
                continue;
            }

            NetworkConnectionSnapshot? listener = FindListener(
                allConnections,
                processId,
                connection.Protocol,
                connection.RemotePort);
            if (listener is not null)
            {
                NetworkPathResolution? resolution = TryCreateLocalProxyResolution(listener, allConnections, processNames);
                if (resolution is not null)
                    return resolution;
            }
        }

        return null;
    }

    private static NetworkConnectionSnapshot? FindListener(
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        int processId,
        string protocol,
        int port) =>
        connections.FirstOrDefault(connection =>
            connection.IsListener &&
            connection.ProcessId != processId &&
            connection.LocalPort == port &&
            string.Equals(connection.Protocol, protocol, StringComparison.OrdinalIgnoreCase));

    private NetworkPathResolution? TryCreateLocalProxyResolution(
        NetworkConnectionSnapshot listener,
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        IReadOnlyDictionary<int, string> processNames)
    {
        processNames.TryGetValue(listener.ProcessId, out string? proxyName);
        proxyName ??= $"PID {listener.ProcessId}";
        bool configuredPort = _systemProxyProvider.GetConfiguredLoopbackPorts().Contains(listener.LocalPort);
        bool knownProxy = IsKnownProxyProcess(proxyName);
        bool hasExternalConnection = connections.Any(connection =>
            connection.ProcessId == listener.ProcessId
            && !connection.IsListener
            && IPAddress.TryParse(connection.RemoteAddress, out IPAddress? remote)
            && !IPAddress.IsLoopback(remote)
            && !remote.Equals(IPAddress.Any)
            && !remote.Equals(IPAddress.IPv6Any));

        int evidence = (configuredPort ? 2 : 0) + (knownProxy ? 1 : 0) + (hasExternalConnection ? 1 : 0);
        if (evidence < 3)
            return null;

        ProxyDetectionConfidence confidence = configuredPort && knownProxy && hasExternalConnection
            ? ProxyDetectionConfidence.High
            : ProxyDetectionConfidence.Medium;
        return new(NetworkPathKind.LocalProxy, "Loopback", proxyName, false, confidence);
    }

    private static bool IsKnownProxyProcess(string processName) =>
        processName.Contains("clash", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("mihomo", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("v2ray", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("sing-box", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("shadowsocks", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("trojan", StringComparison.OrdinalIgnoreCase)
        || processName.Contains("surge", StringComparison.OrdinalIgnoreCase);
}
