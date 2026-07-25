using WindowsToolbox.Modules.InstalledApps.Models;
using WindowsToolbox.Modules.InstalledApps.Services;
using WindowsToolbox.Modules.InstalledApps.Utilities;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class InstalledAppsUtilityTests
{
    [TestMethod]
    public void SizeFormatter_FormatsExpectedUnits()
    {
        Assert.AreEqual("未知", SizeFormatter.Format(null));
        Assert.AreEqual("512 B", SizeFormatter.Format(512));
        Assert.AreEqual("1 KB", SizeFormatter.Format(1024));
        Assert.AreEqual("1.5 MB", SizeFormatter.Format(1_572_864));
    }

    [TestMethod]
    public void QuotedUninstallPathWithSpaces_IsParsed()
    {
        UninstallCommand? command = UninstallCommandParser.Parse(
            "\"C:\\Program Files\\Demo App\\uninstall.exe\" /remove /ui",
            _ => true);

        Assert.IsNotNull(command);
        Assert.AreEqual(@"C:\Program Files\Demo App\uninstall.exe", command.ExecutablePath);
        Assert.AreEqual("/remove /ui", command.Arguments);
    }

    [TestMethod]
    public void MsiUninstallCommand_IsParsedUsingSystemExecutable()
    {
        UninstallCommand? command = UninstallCommandParser.Parse(
            "MsiExec.exe /X{01234567-89AB-CDEF-0123-456789ABCDEF}",
            _ => true);

        Assert.IsNotNull(command);
        Assert.AreEqual("msiexec.exe", Path.GetFileName(command.ExecutablePath), true);
        StringAssert.Contains(command.Arguments, "/X");
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("cmd.exe /c remove-all")]
    [DataRow("not-an-executable")]
    [DataRow("\"C:\\Broken Path\\uninstall.exe /x")]
    [DataRow("\"C:\\Apps\\uninstall.exe\" \"unterminated")]
    public void InvalidUninstallCommands_AreRejected(string value)
    {
        UninstallCommand? command = UninstallCommandParser.Parse(value, _ => true);
        Assert.IsNull(command);
    }

    [TestMethod]
    public void ArgumentSplitter_PreservesQuotedValues()
    {
        IReadOnlyList<string> values =
            ApplicationActionService.SplitArguments("/remove \"C:\\Program Files\\Demo App\"");

        CollectionAssert.AreEqual(
            new[] { "/remove", @"C:\Program Files\Demo App" },
            values.ToArray());
    }

    [TestMethod]
    public void SizeScan_SkipsReparsePoints()
    {
        Assert.IsTrue(ApplicationSizeService.ShouldSkip(FileAttributes.ReparsePoint));
        Assert.IsTrue(ApplicationSizeService.ShouldSkip(
            FileAttributes.Directory | FileAttributes.ReparsePoint));
        Assert.IsFalse(ApplicationSizeService.ShouldSkip(FileAttributes.Directory));
    }

    [TestMethod]
    public void CacheInvalidatesWhenVersionOrLocationChanges()
    {
        InstalledApplication app = new()
        {
            Id = "app",
            DisplayName = "App",
            DisplayVersion = "2.0",
            InstallLocation = @"C:\Apps\App"
        };
        CachedApplicationSize valid = new("app", @"C:\Apps\App", "2.0", 100, DateTime.Now);
        CachedApplicationSize oldVersion = valid with { ApplicationVersion = "1.0" };
        CachedApplicationSize oldPath = valid with { InstallLocation = @"C:\Old\App" };

        Assert.IsTrue(ApplicationSizeCache.IsValid(valid, app));
        Assert.IsFalse(ApplicationSizeCache.IsValid(oldVersion, app));
        Assert.IsFalse(ApplicationSizeCache.IsValid(oldPath, app));
    }

    [TestMethod]
    public async Task DirectoryScan_CountsFilesInTemporaryDirectory()
    {
        string root = Path.Combine(Path.GetTempPath(), "WindowsToolboxTests", Guid.NewGuid().ToString("N"));
        string cachePath = Path.Combine(root, "cache.json");
        string installPath = Path.Combine(root, "app");
        Directory.CreateDirectory(Path.Combine(installPath, "data"));
        try
        {
            await File.WriteAllBytesAsync(Path.Combine(installPath, "one.bin"), new byte[1024]);
            await File.WriteAllBytesAsync(Path.Combine(installPath, "data", "two.bin"), new byte[2048]);
            InstalledApplication app = new()
            {
                Id = "temporary-app",
                DisplayName = "Temporary App",
                DisplayVersion = "1.0",
                InstallLocation = installPath
            };
            ApplicationSizeService service = new(new ApplicationSizeCache(cachePath), 1);

            ApplicationSizeInfo result =
                await service.ScanAsync(app, null, CancellationToken.None);

            Assert.AreEqual(3072L, result.SizeBytes);
            Assert.AreEqual(2L, result.FileCount);
            Assert.IsTrue(File.Exists(cachePath));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
