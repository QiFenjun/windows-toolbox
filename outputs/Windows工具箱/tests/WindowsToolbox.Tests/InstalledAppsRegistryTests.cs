using WindowsToolbox.Modules.InstalledApps.Models;
using WindowsToolbox.Modules.InstalledApps.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class InstalledAppsRegistryTests
{
    [TestMethod]
    public void RegistryFields_AreConvertedCorrectly()
    {
        Dictionary<string, object?> values = new()
        {
            ["DisplayName"] = "示例软件",
            ["DisplayVersion"] = "2.5",
            ["Publisher"] = "示例发布者",
            ["InstallDate"] = "20260724",
            ["InstallLocation"] = @"C:\Apps\Example",
            ["EstimatedSize"] = 2048,
            ["UninstallString"] = "\"C:\\Apps\\Example\\uninstall.exe\" /remove",
            ["WindowsInstaller"] = 1,
            ["SystemComponent"] = 0,
            ["NoRemove"] = 0
        };

        InstalledApplication? app = RegistryEntryMapper.Map(
            values,
            @"LocalMachine\Registry64\...\Example",
            "x64",
            true);

        Assert.IsNotNull(app);
        Assert.AreEqual("示例软件", app.DisplayName);
        Assert.AreEqual("2.5", app.DisplayVersion);
        Assert.AreEqual("示例发布者", app.Publisher);
        Assert.AreEqual(new DateTime(2026, 7, 24), app.InstallDate);
        Assert.AreEqual(2048L * 1024L, app.ReportedSizeBytes);
        Assert.IsTrue(app.CanUninstall);
        Assert.IsTrue(app.RequiresElevation);
    }

    [TestMethod]
    public void EstimatedSize_IsConvertedFromKilobytesToBytes()
    {
        Dictionary<string, object?> values = new()
        {
            ["DisplayName"] = "Size App",
            ["EstimatedSize"] = 1234
        };

        InstalledApplication? app = RegistryEntryMapper.Map(values, "path", "x86", false);

        Assert.IsNotNull(app);
        Assert.AreEqual(1_263_616L, app.ReportedSizeBytes);
    }

    [TestMethod]
    public void EmptyDisplayName_IsRejected()
    {
        InstalledApplication? app = RegistryEntryMapper.Map(
            new Dictionary<string, object?> { ["DisplayVersion"] = "1.0" },
            "path",
            "x64",
            false);

        Assert.IsNull(app);
    }

    [TestMethod]
    public void SystemComponent_IsHiddenByDefault()
    {
        InstalledApplication normal = CreateApp("Normal", false);
        InstalledApplication system = CreateApp("System", true);

        IReadOnlyList<InstalledApplication> filtered =
            InstalledAppService.FilterSystemComponents([normal, system], false);

        Assert.AreEqual(1, filtered.Count);
        Assert.AreEqual("Normal", filtered[0].DisplayName);
    }

    [TestMethod]
    public void Duplicate32And64BitEntries_AreMergedUsingCompositeIdentity()
    {
        InstalledApplication x64 = CreateApp("Same App", false, "x64", "1.0", "Vendor", "uninstall.exe /x");
        InstalledApplication x86 = CreateApp("Same App", false, "x86", "1.0", "Vendor", "uninstall.exe /x");

        IReadOnlyList<InstalledApplication> merged =
            InstalledAppDeduplicator.Merge([x64, x86]);

        Assert.AreEqual(1, merged.Count);
        StringAssert.Contains(merged[0].Architecture, "x64");
        StringAssert.Contains(merged[0].Architecture, "x86");
    }

    [TestMethod]
    public void SameNameWithDifferentVersion_IsNotMerged()
    {
        InstalledApplication first = CreateApp("Same App", false, "x64", "1.0", "Vendor", "one.exe");
        InstalledApplication second = CreateApp("Same App", false, "x64", "2.0", "Vendor", "two.exe");

        IReadOnlyList<InstalledApplication> merged =
            InstalledAppDeduplicator.Merge([first, second]);

        Assert.AreEqual(2, merged.Count);
    }

    private static InstalledApplication CreateApp(
        string name,
        bool system,
        string architecture = "x64",
        string version = "1.0",
        string publisher = "Vendor",
        string uninstall = "uninstall.exe") =>
        new()
        {
            Id = $"{name}-{version}-{architecture}",
            DisplayName = name,
            DisplayVersion = version,
            Publisher = publisher,
            Architecture = architecture,
            UninstallString = uninstall,
            CanUninstall = true,
            IsSystemComponent = system,
            IsSystemEntry = system,
            RegistryPath = $"path-{architecture}"
        };
}
