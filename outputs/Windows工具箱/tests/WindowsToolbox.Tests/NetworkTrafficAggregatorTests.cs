using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsToolbox.Modules.NetworkTraffic.Models;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class NetworkTrafficAggregatorTests
{
    [TestMethod]
    public void UploadAndDownloadAccumulateForCurrentProcess()
    {
        NetworkTrafficAggregator aggregator = new();
        int processId = Environment.ProcessId;
        aggregator.Add(Event(processId, TrafficDirection.Upload, 300, "TCP"));
        aggregator.Add(Event(processId, TrafficDirection.Download, 700, "TCP"));
        ApplicationTrafficGroup group = aggregator.Snapshot([], [], "下载速度", "全部", string.Empty).Single();
        Assert.AreEqual(300L, group.SessionUploadBytes);
        Assert.AreEqual(700L, group.SessionDownloadBytes);
    }

    [TestMethod]
    public void UdpEventsAreIncludedInSessionTotals()
    {
        NetworkTrafficAggregator aggregator = new();
        aggregator.Add(Event(Environment.ProcessId, TrafficDirection.Download, 80, "UDP"));
        ApplicationTrafficGroup group = aggregator.Snapshot([], [], "下载速度", "全部", string.Empty).Single();
        Assert.AreEqual(80L, group.SessionDownloadBytes);
    }

    [TestMethod]
    public void ZeroSizedEventsAreIgnored()
    {
        NetworkTrafficAggregator aggregator = new();
        aggregator.Add(Event(Environment.ProcessId, TrafficDirection.Download, 0, "TCP"));
        Assert.AreEqual(0, aggregator.Snapshot([], [], "下载速度", "全部", string.Empty).Count);
    }

    [TestMethod]
    public void SearchFiltersUnmatchedGroup()
    {
        NetworkTrafficAggregator aggregator = new();
        aggregator.Add(Event(Environment.ProcessId, TrafficDirection.Download, 1, "TCP"));
        Assert.AreEqual(0, aggregator.Snapshot([], [], "名称", "全部", "__no_match__").Count);
    }

    [TestMethod]
    public void SystemFilterDoesNotTreatCurrentTestProcessAsSystem()
    {
        NetworkTrafficAggregator aggregator = new();
        aggregator.Add(Event(Environment.ProcessId, TrafficDirection.Download, 1, "TCP"));
        Assert.AreEqual(0, aggregator.Snapshot([], [], "名称", "系统", string.Empty).Count);
    }

    [TestMethod]
    public void ProcessIdentityIncludesStartTime()
    {
        TrafficProcessIdentity one = new(100, DateTimeOffset.UnixEpoch);
        TrafficProcessIdentity two = new(100, DateTimeOffset.UnixEpoch.AddSeconds(1));
        Assert.AreNotEqual(one, two);
    }

    [TestMethod]
    public void ConnectionSnapshotProviderReturnsWithoutChangingSystemNetworkConfiguration()
    {
        IpHelperConnectionSnapshotProvider provider = new();
        IReadOnlyList<NetworkConnectionSnapshot> result = provider.Read();
        Assert.IsNotNull(result);
    }

    private static NetworkTrafficEvent Event(int processId, TrafficDirection direction, long bytes, string protocol) =>
        new(DateTimeOffset.UtcNow, processId, protocol, direction, bytes, "10.0.0.2", 40000, "8.8.8.8", 443);
}
