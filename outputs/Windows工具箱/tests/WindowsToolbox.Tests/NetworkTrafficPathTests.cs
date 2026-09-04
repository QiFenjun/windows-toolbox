using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsToolbox.Modules.NetworkTraffic.Models;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class NetworkTrafficPathTests
{
    private readonly NetworkPathResolver _resolver = new(systemProxyProvider: new StaticProxyProvider());

    [TestMethod]
    public void LoopbackWithoutListenerIsMarkedLoopback()
    {
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "127.0.0.1", 5000, "127.0.0.1", 7890);
        NetworkPathResolution result = _resolver.Resolve(item, [], new Dictionary<int, string>());
        Assert.AreEqual(NetworkPathKind.Loopback, result.Kind);
    }

    [TestMethod]
    public void Ipv6LoopbackWithoutListenerIsMarkedLoopback()
    {
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "::1", 5000, "::1", 7890);
        NetworkPathResolution result = _resolver.Resolve(item, [], new Dictionary<int, string>());
        Assert.AreEqual(NetworkPathKind.Loopback, result.Kind);
    }

    [TestMethod]
    public void OrdinaryLoopbackServiceIsNotMarkedAsProxy()
    {
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "127.0.0.1", 5000, "127.0.0.1", 7890);
        NetworkConnectionSnapshot listener = new(20, "TCP", "127.0.0.1", 7890, "0.0.0.0", 0, "Listen", true);
        NetworkPathResolution result = _resolver.Resolve(item, [listener], new Dictionary<int, string> { [20] = "LocalDevServer" });
        Assert.AreEqual(NetworkPathKind.Loopback, result.Kind);
    }

    [TestMethod]
    public void ProcessLoopbackConnectionMarksProxyWhenLastEtwEventIsDirect()
    {
        NetworkTrafficEvent lastEvent = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "10.0.0.2", 5000, "1.1.1.1", 443);
        NetworkConnectionSnapshot browserConnection = new(10, "TCP", "127.0.0.1", 52000, "127.0.0.1", 7897, "Established");
        NetworkConnectionSnapshot proxyListener = new(20, "TCP", "127.0.0.1", 7897, "0.0.0.0", 0, "Listen", true);
        NetworkConnectionSnapshot proxyExternal = new(20, "TCP", "10.0.0.2", 53000, "1.1.1.1", 443, "Established");

        NetworkPathResolver resolver = new(systemProxyProvider: new StaticProxyProvider(7897));
        NetworkPathResolution result = resolver.Resolve(
            lastEvent,
            [browserConnection, proxyListener, proxyExternal],
            new Dictionary<int, string> { [20] = "verge-mihomo" },
            processConnections: [browserConnection]);

        Assert.AreEqual(NetworkPathKind.LocalProxy, result.Kind);
        Assert.AreEqual("verge-mihomo", result.ProxyProcessName);
        Assert.AreEqual(ProxyDetectionConfidence.High, result.ProxyConfidence);
    }

    [TestMethod]
    public void SystemProxyParserSupportsSingleAndProtocolSpecificLoopbackEndpoints()
    {
        IReadOnlySet<int> ports = SystemProxyConfigurationProvider.ParseLoopbackPorts(
            "http=127.0.0.1:7897;https=[::1]:7898;socks=192.0.2.10:1080");

        CollectionAssert.AreEquivalent(new[] { 7897, 7898 }, ports.ToArray());
    }

    [TestMethod]
    public void ExternalAddressIsUnknownUntilRouteResolutionIsAvailable()
    {
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "UDP", TrafficDirection.Upload, 10, "10.0.0.2", 5000, "8.8.8.8", 53);
        NetworkPathResolution result = _resolver.Resolve(item, [], new Dictionary<int, string>());
        Assert.AreEqual(NetworkPathKind.Unknown, result.Kind);
        Assert.IsTrue(result.IsAttributionUncertain);
    }

    [TestMethod]
    public void RoutedTunnelConnectionIsMarkedVpn()
    {
        NetworkInterfaceSnapshot tunnel = new("tun", 10, "Wintun", "Wintun Userspace Tunnel", "Tunnel", NetworkPathKind.Vpn, true, 0, 0, 0, 0);
        NetworkPathResolver resolver = new(new StaticRouteResolver(tunnel));
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "10.0.0.2", 5000, "1.1.1.1", 443);
        NetworkPathResolution result = resolver.Resolve(item, [], new Dictionary<int, string>(), [tunnel]);
        Assert.AreEqual(NetworkPathKind.Vpn, result.Kind);
        Assert.AreEqual("Wintun", result.InterfaceName);
    }

    [TestMethod]
    public void RoutedPhysicalConnectionIsMarkedDirect()
    {
        NetworkInterfaceSnapshot ethernet = new("eth", 5, "Ethernet", "Adapter", "以太网", NetworkPathKind.Direct, true, 0, 0, 0, 0);
        NetworkPathResolver resolver = new(new StaticRouteResolver(ethernet));
        NetworkTrafficEvent item = new(DateTimeOffset.UtcNow, 10, "TCP", TrafficDirection.Upload, 10, "10.0.0.2", 5000, "1.1.1.1", 443);
        NetworkPathResolution result = resolver.Resolve(item, [], new Dictionary<int, string>(), [ethernet]);
        Assert.AreEqual(NetworkPathKind.Direct, result.Kind);
    }

    private sealed class StaticRouteResolver(NetworkInterfaceSnapshot networkInterface) : IInterfaceRouteResolver
    {
        public bool TryResolve(string destinationAddress, IReadOnlyList<NetworkInterfaceSnapshot> interfaces, out NetworkInterfaceSnapshot result)
        {
            result = networkInterface;
            return true;
        }
    }

    private sealed class StaticProxyProvider(params int[] ports) : ISystemProxyConfigurationProvider
    {
        public IReadOnlySet<int> GetConfiguredLoopbackPorts() => ports.ToHashSet();
    }
}
