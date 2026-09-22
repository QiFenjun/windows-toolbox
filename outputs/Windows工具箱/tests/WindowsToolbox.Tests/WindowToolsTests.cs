using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.WindowTools;
using WindowsToolbox.Modules.WindowTools.Models;
using WindowsToolbox.Modules.WindowTools.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class WindowToolsTests
{
    [TestMethod]
    public void VisibleTitledWindowIsEnumerated()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot item = Enumerate(platform).Single();
        Assert.AreEqual("Editor", item.Title);
        Assert.AreEqual((uint)424242, item.ProcessId);
    }

    [TestMethod]
    public void HiddenUntitledAndInvalidWindowsAreExcluded()
    {
        FakePlatform platform = CreatePlatform();
        platform.Windows.Add(MakeWindow((nint)2, "Hidden") with { IsVisible = false });
        platform.Windows.Add(MakeWindow((nint)3, ""));
        platform.Windows.Add(MakeWindow(0, "Invalid"));
        Assert.AreEqual(1, Enumerate(platform).Count);
    }

    [TestMethod]
    public void ToolboxHelperIsExcluded()
    {
        FakePlatform platform = CreatePlatform();
        platform.Windows.Add(MakeWindow((nint)2, "helper") with { ClassName = "WindowsToolbox.QuickLaunch.Hotkey" });
        Assert.AreEqual(1, Enumerate(platform).Count);
    }

    [DataTestMethod]
    [DataRow("Progman")]
    [DataRow("WorkerW")]
    [DataRow("Shell_TrayWnd")]
    [DataRow("Shell_SecondaryTrayWnd")]
    public void CriticalShellWindowsAreBlocked(string className)
    {
        WindowSafetyPolicy policy = new();
        WindowNativeSnapshot window = MakeWindow((nint)1, "Shell") with { ClassName = className };
        Assert.IsTrue(policy.IsSystemShellWindow(window));
        Assert.IsFalse(policy.CanModify(window));
    }

    [TestMethod]
    public void NormalExplorerWindowIsAllowed()
    {
        WindowSafetyPolicy policy = new();
        WindowNativeSnapshot explorer = MakeWindow((nint)1, "Documents") with { ClassName = "CabinetWClass", ProcessName = "explorer.exe" };
        Assert.IsTrue(policy.IsListable(explorer));
        Assert.IsTrue(policy.CanModify(explorer));
    }

    [TestMethod]
    public void StaleWindowIsRejectedBeforeOperation()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot snapshot = Enumerate(platform).Single();
        platform.Windows.Clear();
        WindowOperationResult result = Controller(platform).Center(snapshot);
        Assert.IsFalse(result.Success);
        Assert.AreEqual(1400, result.ErrorCode);
    }

    [TestMethod]
    public void ProcessIdMismatchAfterHwndReuseIsRejected()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot snapshot = Enumerate(platform).Single();
        platform.Replace(snapshot.Hwnd, MakeWindow(snapshot.Hwnd, "New") with { ProcessId = 525252 });
        WindowOperationResult result = Controller(platform).Center(snapshot);
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.UserMessage, "变更");
    }

    [TestMethod]
    public void TopMostCanBeSetAndDetected()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot snapshot = Enumerate(platform).Single();
        Assert.IsTrue(Controller(platform).SetTopMost(snapshot, true).Success);
        Assert.IsTrue(Enumerate(platform).Single().IsTopMost);
    }

    [TestMethod]
    public void NativeTopMostCallKeepsNoActivateFlag()
    {
        string source = SourceFile("src", "WindowsToolbox.Modules.WindowTools", "Services", "WindowsWindowPlatform.cs");
        StringAssert.Contains(source, "NativeMethods.SwpNoActivate");
    }

    [TestMethod]
    public void TopMostCanBeRemoved()
    {
        FakePlatform platform = CreatePlatform();
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { IsTopMost = true });
        WindowSnapshot snapshot = Enumerate(platform).Single();
        Assert.IsTrue(Controller(platform).SetTopMost(snapshot, false).Success);
        Assert.IsFalse(Enumerate(platform).Single().IsTopMost);
    }

    [TestMethod]
    public void CenterUsesPrimaryWorkingAreaInsteadOfFullBounds()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot snapshot = Enumerate(platform).Single();
        Assert.IsTrue(Controller(platform).Center(snapshot).Success);
        Assert.AreEqual(new WindowRect(760, 320, 400, 400), platform.Windows.Single().WindowRect);
    }

    [TestMethod]
    public void CenterUsesSecondaryMonitorWorkingArea()
    {
        FakePlatform platform = CreatePlatform();
        platform.Monitors.Add(new MonitorSnapshot((nint)20, "DISPLAY2", "Display 2", new WindowRect(1920, 0, 1600, 900), new WindowRect(1920, 0, 1600, 860), false, 120));
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { MonitorHandle = (nint)20 });
        Assert.IsTrue(Controller(platform).Center(Enumerate(platform).Single()).Success);
        Assert.AreEqual(new WindowRect(2520, 230, 400, 400), platform.Windows.Single().WindowRect);
    }

    [TestMethod]
    public void CenterConstrainsOversizedWindowToWorkingArea()
    {
        FakePlatform platform = CreatePlatform();
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { WindowRect = new WindowRect(-200, -100, 3000, 2000) });
        Assert.IsTrue(Controller(platform).Center(Enumerate(platform).Single()).Success);
        Assert.AreEqual(new WindowRect(0, 0, 1920, 1040), platform.Windows.Single().WindowRect);
    }

    [DataTestMethod]
    [DataRow(1280, 720)]
    [DataRow(1600, 900)]
    [DataRow(1920, 1080)]
    public void ResizeUsesRequestedPhysicalOuterPixels(int width, int height)
    {
        FakePlatform platform = CreatePlatform();
        WindowOperationResult result = Controller(platform).Resize(Enumerate(platform).Single(), width, height);
        Assert.IsTrue(result.Success);
        Assert.AreEqual(Math.Min(width, 1920), platform.Windows.Single().WindowRect.Width);
        Assert.AreEqual(Math.Min(height, 1040), platform.Windows.Single().WindowRect.Height);
    }

    [TestMethod]
    public void CustomSizeIsApplied()
    {
        FakePlatform platform = CreatePlatform();
        Assert.IsTrue(Controller(platform).Resize(Enumerate(platform).Single(), 777, 555).Success);
        Assert.AreEqual(777, platform.Windows.Single().WindowRect.Width);
        Assert.AreEqual(555, platform.Windows.Single().WindowRect.Height);
    }

    [TestMethod]
    public void InvalidWidthAndHeightAreRejected()
    {
        FakePlatform platform = CreatePlatform();
        WindowSnapshot snapshot = Enumerate(platform).Single();
        Assert.IsFalse(Controller(platform).Resize(snapshot, 0, 100).Success);
        Assert.IsFalse(Controller(platform).Resize(snapshot, 100, 0).Success);
    }

    [DataTestMethod]
    [DataRow(WindowStateKind.Minimized)]
    [DataRow(WindowStateKind.Maximized)]
    public void ResizeRestoresMinimizedOrMaximizedWindow(WindowStateKind state)
    {
        FakePlatform platform = CreatePlatform();
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { State = state });
        Assert.IsTrue(Controller(platform).Resize(Enumerate(platform).Single(), 800, 600).Success);
        Assert.IsTrue(platform.RestoreCalled);
        Assert.AreEqual(WindowStateKind.Normal, platform.Windows.Single().State);
    }

    [DataTestMethod]
    [DataRow(WindowLayoutPreset.LeftHalf, 0, 0, 960, 1040)]
    [DataRow(WindowLayoutPreset.RightHalf, 960, 0, 960, 1040)]
    [DataRow(WindowLayoutPreset.TopLeft, 0, 0, 960, 520)]
    [DataRow(WindowLayoutPreset.TopRight, 960, 0, 960, 520)]
    [DataRow(WindowLayoutPreset.BottomLeft, 0, 520, 960, 520)]
    [DataRow(WindowLayoutPreset.BottomRight, 960, 520, 960, 520)]
    public void LayoutPresetsUseWorkingArea(WindowLayoutPreset preset, int left, int top, int width, int height)
    {
        FakePlatform platform = CreatePlatform();
        Assert.IsTrue(Controller(platform).ApplyLayout(Enumerate(platform).Single(), preset).Success);
        Assert.AreEqual(new WindowRect(left, top, width, height), platform.Windows.Single().WindowRect);
    }

    [TestMethod]
    public void MonitorEnumerationIncludesPrimaryMonitor()
    {
        FakePlatform platform = CreatePlatform();
        IReadOnlyList<MonitorSnapshot> monitors = new MonitorService(platform).GetMonitors();
        Assert.AreEqual(1, monitors.Count);
        Assert.IsTrue(monitors.Single().IsPrimary);
    }

    [TestMethod]
    public void MoveToOtherMonitorCentersAndPreservesSize()
    {
        FakePlatform platform = CreatePlatform();
        platform.Monitors.Add(new MonitorSnapshot((nint)20, "DISPLAY2", "Display 2", new WindowRect(1920, 0, 1600, 900), new WindowRect(1920, 0, 1600, 860), false, 96));
        Assert.IsTrue(Controller(platform).MoveToMonitor(Enumerate(platform).Single(), "DISPLAY2").Success);
        Assert.AreEqual(new WindowRect(2520, 230, 400, 400), platform.Windows.Single().WindowRect);
    }

    [TestMethod]
    public void MoveToMonitorConstrainsOversizedWindow()
    {
        FakePlatform platform = CreatePlatform();
        platform.Monitors.Add(new MonitorSnapshot((nint)20, "DISPLAY2", "Display 2", new WindowRect(1920, 0, 800, 600), new WindowRect(1920, 0, 800, 560), false, 96));
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { WindowRect = new WindowRect(0, 0, 1000, 800) });
        Assert.IsTrue(Controller(platform).MoveToMonitor(Enumerate(platform).Single(), "DISPLAY2").Success);
        Assert.AreEqual(new WindowRect(1920, 0, 800, 560), platform.Windows.Single().WindowRect);
    }

    [TestMethod]
    public void ProtectedWindowMapsAccessDeniedToFriendlyMessage()
    {
        FakePlatform platform = CreatePlatform();
        platform.NextErrorCode = 5;
        WindowOperationResult result = Controller(platform).Center(Enumerate(platform).Single());
        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.UserMessage, "权限");
    }

    [TestMethod]
    public void MissingProcessPathDoesNotPreventEnumeration()
    {
        FakePlatform platform = CreatePlatform();
        platform.Replace((nint)1, MakeWindow((nint)1, "Editor") with { ExecutablePath = null });
        Assert.AreEqual(1, Enumerate(platform).Count);
    }

    [TestMethod]
    public void SearchMatchesTitleProcessAndPidCaseInsensitively()
    {
        WindowSnapshot snapshot = Enumerate(CreatePlatform()).Single();
        Assert.IsTrue(WindowSearch.Matches(snapshot, "editor"));
        Assert.IsTrue(WindowSearch.Matches(snapshot, "NOTEpad"));
        Assert.IsTrue(WindowSearch.Matches(snapshot, "424242"));
        Assert.IsTrue(WindowSearch.Matches(snapshot, ""));
    }

    [TestMethod]
    public void ModuleMetadataAndRegistrationAreStable()
    {
        WindowToolsModule module = new();
        ModuleRegistry registry = new();
        registry.Register(module);
        Assert.AreEqual("window-tools", module.Id);
        Assert.AreEqual("窗口工具", module.DisplayName);
        Assert.AreEqual("Window Tools", module.EnglishName);
        Assert.AreEqual("Productivity Tools", module.EnglishCategory);
        Assert.AreSame(module, registry.Find("window-tools"));
    }

    [TestMethod]
    public void WindowToolsViewUsesSharedThemeResources()
    {
        string source = SourceFile("src", "WindowsToolbox.Modules.WindowTools", "Views", "WindowToolsView.xaml");
        StringAssert.Contains(source, "DynamicResource CardStyle");
        StringAssert.Contains(source, "DynamicResource ModernComboBoxStyle");
        StringAssert.Contains(source, "DynamicResource PrimaryButtonStyle");
    }

    private static IReadOnlyList<WindowSnapshot> Enumerate(FakePlatform platform) => new WindowEnumerator(platform, new WindowSafetyPolicy()).Enumerate();
    private static WindowController Controller(FakePlatform platform) => new(platform, new MonitorService(platform), new WindowSafetyPolicy());

    private static FakePlatform CreatePlatform()
    {
        FakePlatform platform = new();
        platform.Monitors.Add(new MonitorSnapshot((nint)10, "DISPLAY1", "Display 1", new WindowRect(0, 0, 1920, 1080), new WindowRect(0, 0, 1920, 1040), true, 96));
        platform.Windows.Add(MakeWindow((nint)1, "Editor"));
        return platform;
    }

    private static WindowNativeSnapshot MakeWindow(nint hwnd, string title) => new(
        hwnd, title, 424242, "notepad.exe", "C:\\Apps\\notepad.exe", "Notepad", new WindowRect(100, 100, 400, 400), new WindowRect(108, 130, 384, 362), true, WindowStateKind.Normal, false, (nint)10, 96);

    private static string SourceFile(params string[] path) =>
        File.ReadAllText(Path.Combine([AppContext.BaseDirectory, "..", "..", "..", "..", "..", .. path]));

    private sealed class FakePlatform : IWindowPlatform
    {
        public List<WindowNativeSnapshot> Windows { get; } = [];
        public List<MonitorSnapshot> Monitors { get; } = [];
        public int NextErrorCode { get; set; }
        public bool RestoreCalled { get; private set; }
        public nint Foreground { get; set; }

        public IReadOnlyList<WindowNativeSnapshot> EnumerateTopLevelWindows() => Windows.ToArray();
        public IReadOnlyList<MonitorSnapshot> EnumerateMonitors() => Monitors.ToArray();
        public nint GetForegroundWindow() => Foreground;
        public bool TryGetWindow(nint hwnd, out WindowNativeSnapshot snapshot)
        {
            snapshot = Windows.FirstOrDefault(window => window.Hwnd == hwnd)!;
            return snapshot is not null && hwnd != 0;
        }

        public bool TryRestore(nint hwnd, out int errorCode)
        {
            RestoreCalled = true;
            errorCode = NextErrorCode;
            if (errorCode != 0) return false;
            return ReplaceState(hwnd, window => window with { State = WindowStateKind.Normal });
        }

        public bool TrySetTopMost(nint hwnd, bool topMost, out int errorCode)
        {
            errorCode = NextErrorCode;
            return errorCode == 0 && ReplaceState(hwnd, window => window with { IsTopMost = topMost });
        }

        public bool TrySetWindowRect(nint hwnd, WindowRect rect, out int errorCode)
        {
            errorCode = NextErrorCode;
            return errorCode == 0 && ReplaceState(hwnd, window => window with { WindowRect = rect });
        }

        public void Replace(nint hwnd, WindowNativeSnapshot value)
        {
            int index = Windows.FindIndex(window => window.Hwnd == hwnd);
            if (index >= 0) Windows[index] = value;
            else Windows.Add(value);
        }

        private bool ReplaceState(nint hwnd, Func<WindowNativeSnapshot, WindowNativeSnapshot> update)
        {
            int index = Windows.FindIndex(window => window.Hwnd == hwnd);
            if (index < 0) return false;
            Windows[index] = update(Windows[index]);
            return true;
        }
    }
}
