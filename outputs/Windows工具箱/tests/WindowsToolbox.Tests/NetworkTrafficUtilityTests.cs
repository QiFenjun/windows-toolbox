using System.Net.NetworkInformation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsToolbox.Modules.NetworkTraffic.Models;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class NetworkTrafficUtilityTests
{
    [DataTestMethod]
    [DataRow(0L, "0 B")]
    [DataRow(1024L, "1.0 KB")]
    [DataRow(1048576L, "1.0 MB")]
    [DataRow(1073741824L, "1.0 GB")]
    public void BytesFormatsExpectedUnits(long value, string expected) =>
        Assert.AreEqual(expected, TrafficDisplayFormatter.Bytes(value));

    [DataTestMethod]
    [DataRow(0d, "0 B/s")]
    [DataRow(1024d, "1.0 KB/s")]
    [DataRow(1024d * 1024d, "1.0 MB/s")]
    public void RatesFormatsExpectedUnits(double value, string expected) =>
        Assert.AreEqual(expected, TrafficDisplayFormatter.Rate(value));

    [TestMethod]
    public void RingBufferDropsOldestValue()
    {
        FixedRingBuffer<int> buffer = new(3);
        buffer.Add(1); buffer.Add(2); buffer.Add(3); buffer.Add(4);
        CollectionAssert.AreEqual(new[] { 2, 3, 4 }, buffer.Snapshot().ToArray());
    }

    [TestMethod]
    public void RingBufferKeepsCapacityAtLeastOne()
    {
        FixedRingBuffer<int> buffer = new(0);
        buffer.Add(1); buffer.Add(2);
        CollectionAssert.AreEqual(new[] { 2 }, buffer.Snapshot().ToArray());
    }

    [DataTestMethod]
    [DataRow(NetworkInterfaceType.Loopback, "Loopback", "", NetworkPathKind.Loopback)]
    [DataRow(NetworkInterfaceType.Tunnel, "Tunnel", "", NetworkPathKind.Vpn)]
    [DataRow(NetworkInterfaceType.Ppp, "PPP", "", NetworkPathKind.Vpn)]
    [DataRow(NetworkInterfaceType.Ethernet, "Wintun", "Virtual Adapter", NetworkPathKind.Vpn)]
    [DataRow(NetworkInterfaceType.Ethernet, "Ethernet", "Intel Adapter", NetworkPathKind.Direct)]
    public void InterfaceClassifierUsesTypeBeforeNameHints(NetworkInterfaceType type, string name, string description, NetworkPathKind expected) =>
        Assert.AreEqual(expected, NetworkInterfaceSnapshotProvider.Classify(type, name, description));
}
