using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Modules.ClipboardPlus;
using WindowsToolbox.Modules.FileTools;
using WindowsToolbox.Modules.InstalledApps;
using WindowsToolbox.Modules.NetworkTraffic;
using WindowsToolbox.Modules.QuickLaunch;
using WindowsToolbox.Modules.QuickLaunch.Models;
using WindowsToolbox.Modules.QuickLaunch.Services;
using WindowsToolbox.Modules.QuickLaunch.ViewModels;
using WindowsToolbox.Modules.Shutdown;
using WindowsToolbox.Modules.Shutdown.Models;
using WindowsToolbox.Modules.Shutdown.Services;
using WindowsToolbox.Modules.TextTools;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class QuickLaunchTests
{
    private string _root = string.Empty;

    [TestInitialize]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), "WindowsToolbox.QuickLaunch.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void TearDown()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); } catch { }
    }

    [TestMethod]
    public void BilingualModuleMetadataKeepsStableIds()
    {
        ISettingsService settings = new FakeSettings();
        IToolModule[] modules =
        [
            new ShutdownModule(new FakeShutdownService(), settings),
            new InstalledAppsModule(),
            new NetworkTrafficModule(settings),
            new ClipboardPlusModule(settings),
            new TextToolsModule(),
            new FileToolsModule(),
            new QuickLaunchModule(settings)
        ];
        CollectionAssert.AreEqual(
            new[] { "shutdown", "installed-apps", "network-traffic", "clipboard-plus", "text-tools", "file-tools", "quick-launch" },
            modules.Select(module => module.Id).ToArray());
        Assert.AreEqual("Shutdown Scheduler", modules[0].EnglishName);
        Assert.AreEqual("App Manager", modules[1].EnglishName);
        Assert.AreEqual("Network Traffic", modules[2].EnglishName);
        Assert.AreEqual("Clipboard+", modules[3].EnglishName);
        Assert.AreEqual("Text Tools", modules[4].EnglishName);
        Assert.AreEqual("File Tools", modules[5].EnglishName);
        Assert.AreEqual("Quick Launch", modules[6].EnglishName);
        Assert.IsTrue(modules.All(module => !string.IsNullOrWhiteSpace(module.EnglishCategory)));
    }

    [TestMethod] public void QuickLaunchMetadataIsStable() { QuickLaunchModule module = new(new FakeSettings()); Assert.AreEqual("quick-launch", module.Id); Assert.AreEqual("快捷启动", module.DisplayName); Assert.AreEqual("Quick Launch", module.EnglishName); Assert.AreEqual("效率工具", module.Category); Assert.AreEqual("Productivity Tools", module.EnglishCategory); }
    [TestMethod] public void ApplicationTargetValidationRequiresExe() { Assert.IsTrue(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Application, "C:\\Apps\\demo.exe")); Assert.IsFalse(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Application, "C:\\Apps\\demo.bat")); }
    [TestMethod] public void FolderTargetValidationRejectsUrl() { Assert.IsTrue(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Folder, "C:\\Work")); Assert.IsFalse(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Folder, "https://example.com")); }
    [TestMethod] public void FileTargetValidationAcceptsMissingPath() => Assert.IsTrue(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.File, "C:\\future\\notes.txt"));
    [TestMethod] public void HttpUrlIsAccepted() => Assert.IsTrue(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Url, "http://example.com"));
    [TestMethod] public void HttpsUrlIsAccepted() => Assert.IsTrue(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Url, "https://example.com/path?q=1"));
    [TestMethod] public void DangerousUrlSchemesAreRejected() { Assert.IsFalse(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Url, "javascript:alert(1)")); Assert.IsFalse(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Url, "file:///C:/a.txt")); Assert.IsFalse(QuickLaunchItemRules.IsValidTarget(QuickLaunchItemType.Url, "shell:AppsFolder")); }
    [TestMethod] public void TypeTextIsBilingual() => StringAssert.Contains(QuickLaunchItemRules.TypeText(QuickLaunchItemType.Application), "Application");
    [TestMethod] public void FallbackGlyphExistsForAllTypes() { foreach (QuickLaunchItemType type in Enum.GetValues<QuickLaunchItemType>()) Assert.IsFalse(string.IsNullOrWhiteSpace(QuickLaunchItemRules.FallbackGlyph(type))); }
    [TestMethod] public void NormalizeKeyIsCaseInsensitiveForPaths() { QuickLaunchItem a = new() { Type = QuickLaunchItemType.File, Target = "C:\\Temp\\A.txt" }; QuickLaunchItem b = new() { Type = QuickLaunchItemType.File, Target = "c:\\temp\\a.txt" }; Assert.AreEqual(QuickLaunchItemRules.NormalizeKey(a), QuickLaunchItemRules.NormalizeKey(b), true); }
    [TestMethod] public void MissingTargetIsReported() { QuickLaunchItem item = new() { Type = QuickLaunchItemType.File, Target = Path.Combine(_root, "missing.txt") }; Assert.IsFalse(QuickLaunchItemRules.TargetExists(item)); }
    [TestMethod] public void ExistingFileTargetIsReported() { string path = CreateFile("note.txt"); QuickLaunchItem item = new() { Type = QuickLaunchItemType.File, Target = path }; Assert.IsTrue(QuickLaunchItemRules.TargetExists(item)); }
    [TestMethod] public void ExistingFolderTargetIsReported() { QuickLaunchItem item = new() { Type = QuickLaunchItemType.Folder, Target = _root }; Assert.IsTrue(QuickLaunchItemRules.TargetExists(item)); }

    [TestMethod]
    public async Task ApplicationItemCanBeSavedWithoutLaunching()
    {
        string exe = CreateFile("demo.exe");
        (QuickLaunchViewModel vm, FakeStore store, _) = CreateViewModel();
        vm.BeginAdd(QuickLaunchItemType.Application, exe);
        vm.EditingName = "Demo";
        await vm.SaveEditAsync();
        Assert.AreEqual(1, vm.ItemCount);
        Assert.AreEqual(1, store.SaveCount);
    }

    [TestMethod]
    public async Task FolderFileAndUrlItemsCanBeAdded()
    {
        string file = CreateFile("note.txt");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([_root, file]);
        vm.BeginAdd(QuickLaunchItemType.Url, "https://example.com");
        vm.EditingName = "Example";
        await vm.SaveEditAsync();
        await Task.Delay(30);
        CollectionAssert.AreEquivalent(new[] { QuickLaunchItemType.Folder, QuickLaunchItemType.File, QuickLaunchItemType.Url }, vm.Items.Select(item => item.Type).ToArray());
    }

    [TestMethod]
    public async Task DroppedExeIsApplication()
    {
        string exe = CreateFile("tool.exe");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([exe]);
        await Task.Delay(30);
        Assert.AreEqual(QuickLaunchItemType.Application, vm.Items.Single().Type);
    }

    [TestMethod]
    public async Task DroppedScriptRemainsFile()
    {
        string script = CreateFile("run.ps1");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([script]);
        await Task.Delay(30);
        Assert.AreEqual(QuickLaunchItemType.File, vm.Items.Single().Type);
    }

    [TestMethod]
    public async Task DuplicateDroppedItemsAreIgnored()
    {
        string file = CreateFile("dup.txt");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([file, file.ToUpperInvariant(), Path.Combine(_root, ".", "dup.txt")]);
        await Task.Delay(30);
        Assert.AreEqual(1, vm.ItemCount);
    }

    [TestMethod]
    public async Task SearchMatchesNameTargetAndGroupCaseInsensitively()
    {
        string file = CreateFile("Report.txt");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.BeginAdd(QuickLaunchItemType.File, file);
        vm.EditingName = "Quarterly Report";
        vm.EditingGroup = "Work";
        await vm.SaveEditAsync();
        vm.SearchText = "quarterly";
        await Task.Delay(220);
        Assert.AreEqual(1, vm.Items.Count);
        vm.SearchText = "REPORT.TXT";
        await Task.Delay(220);
        Assert.AreEqual(1, vm.Items.Count);
        vm.SearchText = "work";
        await Task.Delay(220);
        Assert.AreEqual(1, vm.Items.Count);
    }

    [TestMethod]
    public async Task EmptySearchShowsAllItems()
    {
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([CreateFile("a.txt"), CreateFile("b.txt")]);
        vm.SearchText = string.Empty;
        await Task.Delay(150);
        Assert.AreEqual(2, vm.Items.Count);
    }

    [TestMethod]
    public async Task PinAndUnpinUpdatesPinnedCollection()
    {
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([CreateFile("pin.txt")]);
        await Task.Delay(30);
        QuickLaunchItemViewModel item = vm.Items.Single();
        vm.TogglePinItem(item);
        Assert.AreEqual(1, vm.PinnedItems.Count);
        vm.TogglePinItem(vm.PinnedItems.Single());
        Assert.AreEqual(0, vm.PinnedItems.Count);
    }

    [TestMethod]
    public async Task SuccessfulLaunchUpdatesRecentAndCount()
    {
        (QuickLaunchViewModel vm, _, FakeExecutor executor) = CreateViewModel();
        vm.AddDroppedPaths([CreateFile("launch.txt")]);
        await Task.Delay(30);
        vm.LaunchItem(vm.Items.Single());
        await Task.Delay(50);
        Assert.IsTrue(executor.LaunchCalled);
        Assert.AreEqual(1, vm.Items.Single().LaunchCount);
        Assert.AreEqual(1, vm.RecentItems.Count);
    }

    [TestMethod]
    public async Task FailedLaunchDoesNotIncrementCount()
    {
        (QuickLaunchViewModel vm, _, FakeExecutor executor) = CreateViewModel();
        executor.Success = false;
        vm.AddDroppedPaths([CreateFile("fail.txt")]);
        await Task.Delay(30);
        vm.LaunchItem(vm.Items.Single());
        await Task.Delay(50);
        Assert.AreEqual(0, vm.Items.Single().LaunchCount);
        Assert.AreEqual(0, vm.RecentItems.Count);
    }

    [TestMethod]
    public async Task RemoveOnlyRemovesShortcutRecord()
    {
        string file = CreateFile("keep.txt");
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.AddDroppedPaths([file]);
        await Task.Delay(30);
        vm.RemoveItem(vm.Items.Single());
        Assert.AreEqual(0, vm.ItemCount);
        Assert.IsTrue(File.Exists(file));
    }

    [TestMethod]
    public void GroupCanBeCreatedAndRenamed()
    {
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.CreateGroup("Work");
        Assert.IsTrue(vm.GroupNames.Contains("Work"));
        vm.RenameGroup("Work", "Projects");
        Assert.IsTrue(vm.GroupNames.Contains("Projects"));
    }

    [TestMethod]
    public async Task EnterCanCreateUrlItemThroughEditor()
    {
        (QuickLaunchViewModel vm, _, _) = CreateViewModel();
        vm.BeginAdd(QuickLaunchItemType.Url, "javascript:alert(1)");
        vm.EditingName = "Bad";
        await vm.SaveEditAsync();
        Assert.AreEqual(0, vm.ItemCount);
        StringAssert.Contains(vm.Error, "有效");
    }

    [TestMethod]
    public async Task DataVersionIsPersisted()
    {
        QuickLaunchStore store = new(_root);
        await store.SaveAsync([new QuickLaunchItem { Name = "x", Target = "https://example.com", Type = QuickLaunchItemType.Url }], []);
        string json = await File.ReadAllTextAsync(store.FilePath);
        StringAssert.Contains(json, "DataVersion");
        Assert.AreEqual(1, (await store.LoadAsync()).Items.Count);
    }

    [TestMethod]
    public async Task StoreRoundTripPreservesGroupsAndItems()
    {
        QuickLaunchStore store = new(_root);
        await store.SaveAsync([new QuickLaunchItem { Name = "Example", Target = "https://example.com", Type = QuickLaunchItemType.Url, Group = "Work", IsPinned = true }], [new QuickLaunchGroup { Name = "Work", Order = 0 }]);
        QuickLaunchLoadResult result = await store.LoadAsync();
        Assert.AreEqual("Example", result.Items.Single().Name);
        Assert.IsTrue(result.Items.Single().IsPinned);
        Assert.AreEqual("Work", result.Groups.Single().Name);
    }

    [TestMethod]
    public async Task CorruptStoreIsRecoveredWithoutStartupFailure()
    {
        QuickLaunchStore store = new(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.FilePath)!);
        await File.WriteAllTextAsync(store.FilePath, "{not-json");
        QuickLaunchLoadResult result = await store.LoadAsync();
        Assert.IsTrue(result.WasCorrupt);
        Assert.AreEqual(0, result.Items.Count);
        Assert.IsTrue(result.CorruptBackupPath is null || File.Exists(result.CorruptBackupPath));
    }

    [TestMethod]
    public async Task SaveUsesSingleFinalFile()
    {
        QuickLaunchStore store = new(_root);
        await store.SaveAsync([new QuickLaunchItem { Name = "Example", Target = "https://example.com", Type = QuickLaunchItemType.Url }], []);
        Assert.IsTrue(File.Exists(store.FilePath));
        Assert.AreEqual(0, Directory.GetFiles(Path.GetDirectoryName(store.FilePath)!, "*.tmp").Length);
    }

    [TestMethod]
    public void ExecutorContractDoesNotUseCommandShell()
    {
        string source = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "WindowsToolbox.Modules.QuickLaunch", "Services", "WindowsQuickLaunchExecutor.cs"));
        Assert.IsFalse(source.Contains("cmd.exe", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(source.Contains("powershell -Command", StringComparison.OrdinalIgnoreCase));
    }

    private (QuickLaunchViewModel ViewModel, FakeStore Store, FakeExecutor Executor) CreateViewModel()
    {
        FakeStore store = new();
        FakeExecutor executor = new();
        return (new QuickLaunchViewModel(store, executor, new FakeIconService()), store, executor);
    }

    private string CreateFile(string name)
    {
        string path = Path.Combine(_root, name);
        File.WriteAllText(path, "test");
        return path;
    }

    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public string SettingsFilePath => "memory";
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }

    private sealed class FakeShutdownService : IShutdownService
    {
        public DateTime? ScheduledTime => null;
        public ShutdownOperationResult ValidateShutdownTime(DateTime shutdownTime) => new(true, string.Empty);
        public Task<ShutdownOperationResult> ScheduleShutdownAsync(DateTime shutdownTime, CancellationToken cancellationToken = default) => Task.FromResult(new ShutdownOperationResult(true, string.Empty));
        public Task<ShutdownOperationResult> CancelShutdownAsync(CancellationToken cancellationToken = default) => Task.FromResult(new ShutdownOperationResult(true, string.Empty));
        public TimeSpan? GetRemainingTime() => null;
    }

    private sealed class FakeStore : IQuickLaunchStore
    {
        public string FilePath => "memory";
        public int SaveCount { get; private set; }
        public Task<QuickLaunchLoadResult> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new QuickLaunchLoadResult([], [], false));
        public Task SaveAsync(IEnumerable<QuickLaunchItem> items, IEnumerable<QuickLaunchGroup> groups, CancellationToken cancellationToken = default) { SaveCount++; return Task.CompletedTask; }
    }

    private sealed class FakeExecutor : IQuickLaunchExecutor
    {
        public bool Success { get; set; } = true;
        public bool LaunchCalled { get; private set; }
        public Task<QuickLaunchLaunchResult> LaunchAsync(QuickLaunchItem item, CancellationToken cancellationToken = default) { LaunchCalled = true; return Task.FromResult(new QuickLaunchLaunchResult(Success, Success ? "ok" : "failed")); }
        public Task<QuickLaunchLaunchResult> OpenLocationAsync(QuickLaunchItem item, CancellationToken cancellationToken = default) => Task.FromResult(new QuickLaunchLaunchResult(Success, Success ? "ok" : "failed"));
    }

    private sealed class FakeIconService : IQuickLaunchIconService
    {
        public Task<System.Windows.Media.ImageSource?> GetAsync(QuickLaunchItem item, int size = 40, CancellationToken cancellationToken = default) => Task.FromResult<System.Windows.Media.ImageSource?>(null);
    }
}
