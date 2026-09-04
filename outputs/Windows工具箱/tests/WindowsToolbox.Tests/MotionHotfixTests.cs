using WindowsToolbox.App.Utilities;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class MotionHotfixTests
{
    [TestMethod]
    public void SidebarFinalExpandedWidthIs232()
    {
        Assert.AreEqual(232d, SidebarMotionMetrics.ExpandedWidth);
    }

    [TestMethod]
    public void SidebarFinalCollapsedWidthIs72()
    {
        Assert.AreEqual(72d, SidebarMotionMetrics.CollapsedWidth);
    }

    [TestMethod]
    public void ContentOffsetPreservesVisualPositionInBothDirections()
    {
        Assert.AreEqual(160d, SidebarMotionMetrics.CalculateContentStartOffset(232, 0, 72));
        Assert.AreEqual(-160d, SidebarMotionMetrics.CalculateContentStartOffset(72, 0, 232));
    }

    [TestMethod]
    public void InterruptedExpandThenCollapseEndsAtCollapsedGeometry()
    {
        const double offsetAtInterruption = -80;
        double collapseOffset = SidebarMotionMetrics.CalculateContentStartOffset(232, offsetAtInterruption, 72);

        Assert.AreEqual(80d, collapseOffset);
        Assert.AreEqual(72d, SidebarMotionMetrics.CollapsedWidth);
    }

    [TestMethod]
    public void InterruptedCollapseThenExpandEndsAtExpandedGeometry()
    {
        const double offsetAtInterruption = 80;
        double expandOffset = SidebarMotionMetrics.CalculateContentStartOffset(72, offsetAtInterruption, 232);

        Assert.AreEqual(-80d, expandOffset);
        Assert.AreEqual(232d, SidebarMotionMetrics.ExpandedWidth);
    }

    [TestMethod]
    public void RecentSectionExpandUsesDelayedFadeTimeline()
    {
        Assert.AreEqual(90d, SidebarMotionMetrics.RecentExpandDelay.TotalMilliseconds);
        Assert.AreEqual(110d, SidebarMotionMetrics.RecentFadeInDuration.TotalMilliseconds);
        Assert.AreEqual(4d, SidebarMotionMetrics.RecentTranslateOffset(ReducedMotionMode.Full));
    }

    [TestMethod]
    public void RecentSectionCollapseFadesBeforeGeometryMotion()
    {
        Assert.AreEqual(SidebarMotionMetrics.TextFadeOutDuration, SidebarMotionMetrics.RecentFadeOutDuration);
        Assert.IsTrue(SidebarMotionMetrics.RecentFadeOutDuration < SidebarMotionMetrics.GeometryDuration);
    }

    [TestMethod]
    public void ReducedMotionRecentUsesShorterFadeAndSmallerOffset()
    {
        Assert.AreEqual(TimeSpan.FromMilliseconds(55), SidebarMotionMetrics.Scale(
            SidebarMotionMetrics.RecentFadeInDuration,
            ReducedMotionMode.Reduced));
        Assert.AreEqual(2d, SidebarMotionMetrics.RecentTranslateOffset(ReducedMotionMode.Reduced));
    }

    [TestMethod]
    public void MotionOffKeepsFinalStateImmediate()
    {
        Assert.AreEqual(TimeSpan.Zero, SidebarMotionMetrics.Scale(
            SidebarMotionMetrics.GeometryDuration,
            ReducedMotionMode.Off));
        Assert.AreEqual(232d, SidebarMotionMetrics.ExpandedWidth);
        Assert.AreEqual(72d, SidebarMotionMetrics.CollapsedWidth);
    }

    [TestMethod]
    public void WindowStateMotionIsNotBoundToOrdinaryResize()
    {
        string code = ReadProjectFile("src", "WindowsToolbox.App", "MainWindow.xaml.cs");

        Assert.IsFalse(code.Contains("SizeChanged", StringComparison.Ordinal));
        StringAssert.Contains(code, "DispatcherPriority.Render");
        StringAssert.Contains(code, "IsMaximizeRestoreTransition");
    }

    [TestMethod]
    public void SidebarMotionDoesNotTouchNetworkBusinessState()
    {
        string code = ReadProjectFile("src", "WindowsToolbox.App", "MainWindow.xaml.cs");
        string home = ReadProjectFile("src", "WindowsToolbox.App", "Views", "HomeView.xaml");

        Assert.IsFalse(code.Contains("NetworkTraffic", StringComparison.Ordinal));
        Assert.IsFalse(code.Contains("GridLengthAnimation", StringComparison.Ordinal));
        StringAssert.Contains(code, "TranslateTransform.XProperty");
        Assert.IsFalse(home.Contains("Visibility=\"{Binding HasRecentModules", StringComparison.Ordinal));
    }

    private static string ReadProjectFile(params string[] parts)
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WindowsToolbox.sln")))
            directory = directory.Parent;

        Assert.IsNotNull(directory, "未找到 WindowsToolbox.sln。");
        string path = Path.Combine(new[] { directory!.FullName }.Concat(parts).ToArray());
        Assert.IsTrue(File.Exists(path), $"文件不存在：{path}");
        return File.ReadAllText(path);
    }
}
