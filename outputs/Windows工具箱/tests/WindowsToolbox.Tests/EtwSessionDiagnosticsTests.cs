using Microsoft.VisualStudio.TestTools.UnitTesting;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class EtwSessionDiagnosticsTests
{
    [TestMethod]
    public void SnapshotCountsSystemAndWindowsToolboxSessions()
    {
        EtwSessionDiagnosticsSnapshot snapshot = new(
        [
            new("SystemTrace", 0x02000000, 64, 2, 8, 1, 1, true),
            new(EtwSessionNames.NetworkTraffic, 0, 64, 2, 8, 1, 2, false),
            new("WindowsToolbox.NetworkTraffic.1234.ABCD", 0, 64, 2, 8, 1, 3, false),
            new("OtherTool", 0, 64, 2, 8, 1, 4, false)
        ], 0, null);

        Assert.AreEqual(4, snapshot.VisibleSessionCount);
        Assert.AreEqual(1, snapshot.SystemLoggerCount);
        Assert.AreEqual(2, snapshot.WindowsToolboxSessionCount);
    }

    [TestMethod]
    public void LegacySessionIsOnlyAnOrphanCandidateWhenOwnerProcessIsAbsent()
    {
        Assert.IsTrue(EtwSessionNames.IsLegacyOrphanCandidate(
            "WindowsToolbox.NetworkTraffic.1234.7343338d63d84562a39d908c754190e6", processId => processId != 1234));
        Assert.IsFalse(EtwSessionNames.IsLegacyOrphanCandidate(
            "WindowsToolbox.NetworkTraffic.1234.7343338d63d84562a39d908c754190e6", processId => processId == 1234));
        Assert.IsFalse(EtwSessionNames.IsLegacyOrphanCandidate(
            EtwSessionNames.NetworkTraffic, _ => false));
        Assert.IsFalse(EtwSessionNames.IsLegacyOrphanCandidate(
            "WindowsToolbox.NetworkTraffic.1234.not-a-guid", _ => false));
        Assert.IsFalse(EtwSessionNames.IsLegacyOrphanCandidate(
            "WindowsToolbox.NetworkTraffic.0.7343338d63d84562a39d908c754190e6", _ => false));
        Assert.IsFalse(EtwSessionNames.IsLegacyOrphanCandidate(
            "OtherTool.NetworkTraffic.1234.7343338d63d84562a39d908c754190e6", _ => false));
    }

    [TestMethod]
    public void OnlyExactOrLegacyNetworkTrafficNamesAreOwned()
    {
        Assert.IsTrue(EtwSessionNames.IsWindowsToolboxSession(EtwSessionNames.NetworkTraffic));
        Assert.IsTrue(EtwSessionNames.IsWindowsToolboxSession("WindowsToolbox.NetworkTraffic.12.ABCD"));
        Assert.IsFalse(EtwSessionNames.IsWindowsToolboxSession("WindowsToolbox.Diagnostics"));
        Assert.IsFalse(EtwSessionNames.IsWindowsToolboxSession("Other.WindowsToolbox.NetworkTraffic"));
    }

    [TestMethod]
    public void SnapshotDoesNotDoubleCountSameNativeSession()
    {
        EtwSessionDiagnosticsSnapshot snapshot = new(
        [
            new("NT Kernel Logger", 0x02000000, 4, 48, 48, 3, 1, true),
            new("NT Kernel Logger", 0x02000000, 4, 48, 48, 3, 1, true)
        ], 0, null);

        Assert.AreEqual(1, snapshot.VisibleSessionCount);
        Assert.AreEqual(1, snapshot.SystemLoggerCount);
    }

    [TestMethod]
    public async Task NamedMutexLeaseRejectsSecondOwnerAndAllowsAcquireAfterRelease()
    {
        string name = $"Local\\WindowsToolbox.Tests.NetworkTraffic.{Guid.NewGuid():N}";
        using NamedMutexLease? first = await NamedMutexLease.TryAcquireAsync(name, CancellationToken.None);
        Assert.IsNotNull(first);

        using NamedMutexLease? second = await NamedMutexLease.TryAcquireAsync(name, CancellationToken.None);
        Assert.IsNull(second);

        first.Dispose();
        using NamedMutexLease? afterRelease = await NamedMutexLease.TryAcquireAsync(name, CancellationToken.None);
        Assert.IsNotNull(afterRelease);
    }
}
