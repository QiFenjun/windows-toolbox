using System.Text.Json;
using WindowsToolbox.App.Services;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class UiThemeResourceTests
{
    [TestMethod]
    public void MotionDictionaryContainsSharedTimingAndEasingTokens()
    {
        string motion = ReadProjectFile("src", "WindowsToolbox.App", "Themes", "Motion.xaml");

        StringAssert.Contains(motion, "MotionDurationFast");
        StringAssert.Contains(motion, "MotionDurationNormal");
        StringAssert.Contains(motion, "MotionDurationSlow");
        StringAssert.Contains(motion, "StandardEase");
        StringAssert.Contains(motion, "EmphasizedEase");
    }

    [TestMethod]
    public void ScrollBarDictionaryUsesOneImplicitThemeStyle()
    {
        string scrollBar = ReadProjectFile("src", "WindowsToolbox.App", "Themes", "ScrollBarStyles.xaml");

        StringAssert.Contains(scrollBar, "<Style TargetType=\"ScrollBar\">");
        StringAssert.Contains(scrollBar, "ScrollBarTrackBrush");
        StringAssert.Contains(scrollBar, "ScrollBarThumbHoverBrush");
        StringAssert.Contains(scrollBar, "ScrollBarThumbPressedBrush");
        StringAssert.Contains(scrollBar, "IsDragging");
    }

    [TestMethod]
    public void BothThemeDictionariesDefineScrollbarBrushes()
    {
        string dark = ReadProjectFile("src", "WindowsToolbox.App", "Themes", "Colors.Dark.xaml");
        string light = ReadProjectFile("src", "WindowsToolbox.App", "Themes", "Colors.Light.xaml");

        foreach (string key in new[] { "ScrollBarTrackBrush", "ScrollBarThumbBrush", "ScrollBarThumbHoverBrush", "ScrollBarThumbPressedBrush" })
        {
            StringAssert.Contains(dark, key);
            StringAssert.Contains(light, key);
        }
    }

    [TestMethod]
    public void AppResourcesMergeMotionAndScrollbarDictionaries()
    {
        string app = ReadProjectFile("src", "WindowsToolbox.App", "App.xaml");

        StringAssert.Contains(app, "Themes/Motion.xaml");
        StringAssert.Contains(app, "Themes/ScrollBarStyles.xaml");
    }

    [TestMethod]
    public void ComboBoxStillUsesCustomTemplateAndDynamicSurfaceStates()
    {
        string controls = ReadProjectFile("src", "WindowsToolbox.App", "Themes", "ControlStyles.xaml");

        StringAssert.Contains(controls, "ModernComboBoxStyle");
        StringAssert.Contains(controls, "SurfaceHoverBrush");
        StringAssert.Contains(controls, "FocusVisualStyle");
    }

    [TestMethod]
    public void MainWindowEnablesPixelAlignedDisplayRenderingWithoutScaleAnimation()
    {
        string window = ReadProjectFile("src", "WindowsToolbox.App", "MainWindow.xaml");
        string codeBehind = ReadProjectFile("src", "WindowsToolbox.App", "MainWindow.xaml.cs");

        StringAssert.Contains(window, "UseLayoutRounding=\"True\"");
        StringAssert.Contains(window, "SnapsToDevicePixels=\"True\"");
        StringAssert.Contains(window, "TextOptions.TextFormattingMode=\"Display\"");
        Assert.IsFalse(window.Contains("ScaleTransform", StringComparison.Ordinal));
        Assert.IsFalse(codeBehind.Contains("ScaleTransform", StringComparison.Ordinal));
    }

    [TestMethod]
    public void MotionServiceHonorsFullReducedAndOffModes()
    {
        TimeSpan full = TimeSpan.FromMilliseconds(180);
        MotionService service = new(ReducedMotionMode.Full);
        Assert.AreEqual(full, service.GetDuration(full));

        service.SetMode(ReducedMotionMode.Reduced);
        Assert.AreEqual(TimeSpan.FromMilliseconds(90), service.GetDuration(full));

        service.SetMode(ReducedMotionMode.Off);
        Assert.AreEqual(TimeSpan.Zero, service.GetDuration(full));
    }

    [TestMethod]
    public void ReducedMotionSettingRoundTripsThroughJson()
    {
        AppSettings settings = new() { ReducedMotion = ReducedMotionMode.Reduced };
        string json = JsonSerializer.Serialize(settings);
        AppSettings? restored = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.IsNotNull(restored);
        Assert.AreEqual(ReducedMotionMode.Reduced, restored!.ReducedMotion);
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
