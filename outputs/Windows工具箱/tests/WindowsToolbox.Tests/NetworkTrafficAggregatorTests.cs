using System.Diagnostics;
using System.Text.Json;
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
    public void EventsWithoutAnAttributedProcessAreIgnored()
    {
        NetworkTrafficAggregator aggregator = new();
        aggregator.Add(Event(-1, TrafficDirection.Download, 1024, "TCP"));

        Assert.AreEqual(0, aggregator.Snapshot([], [], "下载速度", "全部", string.Empty).Count);
    }

    [DataTestMethod]
    [DataRow(-1, 1234, 1234)]
    [DataRow(4321, 0, 4321)]
    [DataRow(-1, -1, 0)]
    public void EtwProcessIdPrefersPayloadAndNeverReturnsNegative(
        int headerProcessId,
        int payloadProcessId,
        int expectedProcessId)
    {
        Assert.AreEqual(expectedProcessId, EtwTrafficEventSource.ResolveProcessId(headerProcessId, payloadProcessId));
    }

    [TestMethod]
    public async Task HistoryLoadRemovesLegacyInvalidPidRecordsAndKeepsValidApplication()
    {
        string directory = Path.Combine(Path.GetTempPath(), "WindowsToolboxHistoryTests", Guid.NewGuid().ToString("N"));
        string filePath = Path.Combine(directory, "daily-traffic.json");
        Directory.CreateDirectory(directory);
        string today = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        DailyTrafficRecord valid = new(today, "browser-hash", "Browser", "browser-hash", 100, 20);
        try
        {
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(new DailyTrafficRecord[]
            {
                valid,
                new(today, "old-one", "PID -1", "old-one", 1_000_000, 10),
                new(today, "PID 0", "PID 0", "PID 0", 500, 50),
                new(today, "unknown", "Unknown PID", "unknown", 500, 50)
            }));
            TrafficHistoryStore store = new(filePath);

            IReadOnlyDictionary<string, DailyTrafficRecord> loaded = await store.LoadTodayAsync(CancellationToken.None);
            DailyTrafficRecord[] persisted = JsonSerializer.Deserialize<DailyTrafficRecord[]>(await File.ReadAllTextAsync(filePath))!;

            Assert.AreEqual(1, loaded.Count);
            Assert.AreEqual(valid, loaded[valid.ApplicationId]);
            CollectionAssert.AreEqual(new[] { valid }, persisted);
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    public void LegitimateUnknownProductNameIsNotTreatedAsInvalidIdentity()
    {
        DailyTrafficRecord record = new(
            "2026-09-04", "A1B2C3D4", "Unknown", "A1B2C3D4", 100, 20,
            TrafficIdentityStatus.Resolved);

        Assert.IsTrue(TrafficHistoryStore.IsValidRecord(record));
    }

    [TestMethod]
    public void ExplicitInvalidIdentityIsRejectedEvenWithPositiveTraffic()
    {
        DailyTrafficRecord record = new(
            "2026-09-04", "legacy", "Legacy", "legacy", 100, 20,
            TrafficIdentityStatus.Invalid);

        Assert.IsFalse(TrafficHistoryStore.IsValidRecord(record));
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
