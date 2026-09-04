using System.Net;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>基于连接快照判断 Loopback 和本地代理；未知情形不做猜测。</summary>
public sealed class NetworkPathResolver
{
    private readonly IInterfaceRouteResolver _routeResolver;

    public NetworkPathResolver(IInterfaceRouteResolver? routeResolver = null) =>
        _routeResolver = routeResolver ?? new IpHelperRouteResolver();

    public NetworkPathResolution Resolve(
        NetworkTrafficEvent trafficEvent,
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        IReadOnlyDictionary<int, string> processNames,
        IReadOnlyList<NetworkInterfaceSnapshot>? interfaces = null)
    {
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

        NetworkConnectionSnapshot? listener = connections.FirstOrDefault(connection =>
            connection.IsListener && connection.LocalPort == remotePort &&
            connection.ProcessId != trafficEvent.ProcessId);

        if (listener is not null)
        {
            processNames.TryGetValue(listener.ProcessId, out string? proxyName);
            return new(NetworkPathKind.LocalProxy, "Loopback", proxyName ?? $"PID {listener.ProcessId}");
        }

        return new(NetworkPathKind.Loopback, "Loopback");
    }
}
