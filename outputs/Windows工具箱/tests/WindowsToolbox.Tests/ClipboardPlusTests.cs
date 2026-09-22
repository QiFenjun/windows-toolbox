using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Modules.ClipboardPlus;
using WindowsToolbox.Modules.ClipboardPlus.Models;
using WindowsToolbox.Modules.ClipboardPlus.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ClipboardPlusTests
{
    private static FakeSettings? _lastSettings;
    [TestMethod] public void Options_AcceptsSmallUnicodeText() => Assert.IsTrue(ClipboardOptions.IsValidText("中文"));
    [TestMethod] public void Options_RejectsEmptyText() => Assert.IsFalse(ClipboardOptions.IsValidText(string.Empty));
    [TestMethod] public void Options_RejectsNullText() => Assert.IsFalse(ClipboardOptions.IsValidText(null));
    [TestMethod] public void Options_RejectsOversizedText() => Assert.IsFalse(ClipboardOptions.IsValidText(new string('a', ClipboardOptions.MaxItemBytes)));
    [TestMethod] public void Options_ListsSupportedCapacities() => CollectionAssert.AreEqual(new[] { 100, 300, 500, 1000 }, ClipboardOptions.Capacities);
    [TestMethod] public void Options_ListsPermanentRetentionAsZero() => Assert.AreEqual(0, ClipboardOptions.RetentionDays[^1]);
    [TestMethod] public void Item_PreviewFlattensLines() => Assert.AreEqual("a b", new ClipboardHistoryItem { Text = "a\r\nb" }.Preview);
    [TestMethod] public void Item_SizeTextUsesBytes() => Assert.AreEqual("6 B", new ClipboardHistoryItem { Text = "abc" }.SizeText);
    [TestMethod] public void Item_DefaultIsNotPinned() => Assert.IsFalse(new ClipboardHistoryItem().IsPinned);
    [TestMethod] public void Store_UsesLocalAppDataClipboardFolder() => StringAssert.Contains(new ClipboardHistoryStore("C:\\Local").FilePath, "WindowsToolbox");
    [TestMethod] public async Task Store_MemoryRoundTrip() { MemoryStore store = new(); var item = new ClipboardHistoryItem { Text = "round trip" }; await store.SaveAsync([item]); Assert.AreEqual("round trip", (await store.LoadAsync())[0].Text); }
    [TestMethod] public async Task Service_LoadsPersistedItems() { MemoryStore store = new(); await store.SaveAsync([new ClipboardHistoryItem { Text = "saved" }]); using ClipboardPlusService service = Create(store); await service.LoadAsync(); Assert.AreEqual("saved", service.Items[0].Text); }
    [TestMethod] public void Service_StartsListener() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); Assert.IsTrue(service.Start()); Assert.IsTrue(listener.Started); }
    [TestMethod] public void Service_StopsListener() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); service.Start(); service.Stop(); Assert.IsFalse(listener.Started); }
    [TestMethod] public void Service_CapturesText() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit("hello"); Assert.AreEqual("hello", service.Items[0].Text); }
    [TestMethod] public void Service_DeduplicatesText() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit("same"); listener.Emit("same"); Assert.AreEqual(1, service.Items.Count); }
    [TestMethod] public void Service_IgnoresOversizedText() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit(new string('x', ClipboardOptions.MaxItemBytes)); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_FiltersExcludedPath() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener, resolver: new FakeResolver(new ClipboardSource("app", "C:\\blocked.exe"))); SettingsFor(service).ClipboardPlusExcludedPaths.Add("C:\\blocked.exe"); listener.Emit("blocked"); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_FiltersExcludedProcess() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener, resolver: new FakeResolver(new ClipboardSource("blocked", null))); SettingsFor(service).ClipboardPlusExcludedProcessNames.Add("blocked"); listener.Emit("blocked"); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_DeleteRemovesItem() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit("delete"); string id = service.Items[0].Id; service.Delete(id); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_PinPreservesItem() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit("pin"); string id = service.Items[0].Id; service.SetPinned(id, true); Assert.IsTrue(service.Items[0].IsPinned); }
    [TestMethod] public void Service_ClearRemovesAllItems() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); listener.Emit("a"); listener.Emit("b"); service.Clear(); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_CapacityPrunesOldestItems() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); SettingsFor(service).ClipboardPlusCapacity = 100; for (int i = 0; i < 101; i++) listener.Emit(i.ToString()); Assert.AreEqual(100, service.Items.Count); }
    [TestMethod] public void Service_PermanentRetentionKeepsItems() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener); SettingsFor(service).ClipboardPlusRetentionDays = 0; listener.Emit("keep"); Assert.AreEqual(1, service.Items.Count); }
    [TestMethod] public void Service_CopyWritesUnicodeText() { FakeAdapter adapter = new(); using ClipboardPlusService service = Create(adapter: adapter); ClipboardHistoryItem item = new() { Text = "copy" }; service.Copy(item); Assert.AreEqual("copy", adapter.LastText); }
    [TestMethod] public void Service_StartFailureRaisesNotice() { FakeListener listener = new(); listener.StartResult = false; List<string> notices = []; using ClipboardPlusService service = Create(listener: listener); service.Notice += (_, message) => notices.Add(message); Assert.IsFalse(service.Start()); Assert.AreEqual(1, notices.Count); }
    [TestMethod] public void Service_HotkeyForwardsEvent() { FakeListener listener = new(); int count = 0; using ClipboardPlusService service = Create(listener: listener); service.HotkeyPressed += (_, _) => count++; listener.EmitHotkey(); Assert.AreEqual(1, count); }
    [TestMethod] public void Service_RegistersHotkeyStatus() { FakeListener listener = new(); listener.HotkeyResult = true; using ClipboardPlusService service = Create(listener: listener); service.Start(); Assert.IsTrue(service.IsHotkeyRegistered); }
    [TestMethod] public void SourceResolverUnknownIsSafe() => Assert.IsNull(new FakeResolver(new ClipboardSource(null, null)).Resolve(0).ProcessName);
    [TestMethod] public void ModuleMetadataUsesStableId() { FakeSettings settings = new(); ClipboardPlusModule module = new(settings); Assert.AreEqual("clipboard-plus", module.Id); Assert.AreEqual("剪贴板+", module.DisplayName); Assert.AreEqual("Clipboard+", module.EnglishName); }
    [TestMethod] public void ModuleBelongsToEfficiencyCategory() { FakeSettings settings = new(); Assert.AreEqual("效率工具", new ClipboardPlusModule(settings).Category); }
    [TestMethod] public void SettingsClipboardDisabledByDefault() => Assert.IsFalse(new AppSettings().ClipboardPlusEnabled);
    [TestMethod] public void SettingsClipboardCapacityDefaultsTo300() => Assert.AreEqual(300, new AppSettings().ClipboardPlusCapacity);
    [TestMethod] public void SettingsClipboardRetentionDefaultsTo30() => Assert.AreEqual(30, new AppSettings().ClipboardPlusRetentionDays);
    [TestMethod] public void Service_SequenceSelfWriteDoesNotDuplicate() { FakeAdapter adapter = new(); FakeListener listener = new(); using ClipboardPlusService service = Create(adapter: adapter, listener: listener); ClipboardHistoryItem item = new() { Text = "self" }; service.Copy(item); listener.Emit("self", adapter.Sequence); Assert.AreEqual(0, service.Items.Count); }
    [TestMethod] public void Service_UnknownOwnerStillCaptures() { FakeListener listener = new(); using ClipboardPlusService service = Create(listener: listener, resolver: new FakeResolver(new ClipboardSource(null, null))); listener.Emit("unknown"); Assert.AreEqual("未知来源", service.Items[0].SourceDisplayName); }

    private static ClipboardPlusService Create(MemoryStore? store = null, FakeAdapter? adapter = null, FakeListener? listener = null, FakeResolver? resolver = null)
    {
        _lastSettings = new FakeSettings();
        return new ClipboardPlusService(adapter ?? new FakeAdapter(), store ?? new MemoryStore(), resolver ?? new FakeResolver(new ClipboardSource("test", "C:\\test.exe")), listener ?? new FakeListener(), _lastSettings);
    }
    private static AppSettings SettingsFor(ClipboardPlusService service) => _lastSettings!.Settings;
 
    private sealed class FakeSettings : ISettingsService
    {
        public AppSettings Settings { get; } = new();
        public string SettingsFilePath => "memory";
        public Task LoadAsync() => Task.CompletedTask;
        public Task SaveAsync() => Task.CompletedTask;
    }
    private sealed class FakeAdapter : IClipboardAdapter
    {
        public string? LastText { get; private set; }
        public uint Sequence { get; private set; }
        public bool ContainsUnicodeText() => LastText is not null;
        public string? GetUnicodeText() => LastText;
        public void SetUnicodeText(string text) { LastText = text; Sequence++; }
        public uint GetSequenceNumber() => Sequence;
        public nint GetOwnerWindow() => 0;
    }
    private sealed class FakeListener : IClipboardListener
    {
        public bool StartResult { get; set; } = true;
        public bool HotkeyResult { get; set; } = true;
        public bool Started { get; private set; }
        public event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
        public event EventHandler? HotkeyPressed;
        public bool IsRunning => Started;
        public bool IsHotkeyRegistered => Started && HotkeyResult;
        public bool Start(bool registerHotkey = true) { Started = StartResult; return StartResult; }
        public void Stop() => Started = false;
        public void Emit(string text, uint sequence = 0) => ClipboardChanged?.Invoke(this, new ClipboardChangedEventArgs(text, sequence, 0));
        public void EmitHotkey() => HotkeyPressed?.Invoke(this, EventArgs.Empty);
        public void Dispose() => Stop();
    }
    private sealed class FakeResolver(ClipboardSource source) : IClipboardSourceResolver
    {
        public ClipboardSource Resolve(nint ownerWindow) => source;
    }
    private sealed class MemoryStore : IClipboardHistoryStore
    {
        private IReadOnlyList<ClipboardHistoryItem> _items = [];
        public string FilePath => "memory";
        public Task<IReadOnlyList<ClipboardHistoryItem>> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(_items);
        public Task SaveAsync(IReadOnlyCollection<ClipboardHistoryItem> items, CancellationToken cancellationToken = default) { _items = items.ToArray(); return Task.CompletedTask; }
    }
}
