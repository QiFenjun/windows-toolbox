using System.Windows.Media;
using WindowsToolbox.Modules.Utilities.Color.Models;
using WindowsToolbox.Modules.Utilities.Color.Services;
using WindowsToolbox.Modules.Utilities.Color.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ColorToolsCoreTests
{
    [DataTestMethod]
    [DataRow("#000000", 255, 0, 0, 0)]
    [DataRow("#FFFFFF", 255, 255, 255, 255)]
    [DataRow("#FF0000", 255, 255, 0, 0)]
    [DataRow("#ff4a6b", 255, 255, 74, 107)]
    [DataRow("#80FF0000", 128, 255, 0, 0)]
    [DataRow("#F00", 255, 255, 0, 0)]
    public void HexParser_ParsesRgbAndArgbForms(string text, int alpha, int red, int green, int blue)
    {
        Assert.IsTrue(ColorConversionService.TryParseHex(text, out ColorValue color));
        Assert.AreEqual(alpha, color.A);
        Assert.AreEqual(red, color.R);
        Assert.AreEqual(green, color.G);
        Assert.AreEqual(blue, color.B);
    }

    [DataTestMethod]
    [DataRow("#")]
    [DataRow("#AB")]
    [DataRow("#ABCDE")]
    [DataRow("#123456789")]
    [DataRow("#GG0000")]
    [DataRow("#FF00XX")]
    [DataRow("#ARGB")]
    public void HexParser_RejectsInvalidForms(string text) =>
        Assert.IsFalse(ColorConversionService.TryParseHex(text, out _));

    [TestMethod]
    public void RgbFactory_ValidatesInclusiveByteBoundaries()
    {
        Assert.AreEqual(new ColorValue(255, 0, 255, 0), ColorConversionService.FromRgb(0, 255, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromRgb(-1, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromRgb(0, 256, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromRgb(0, 0, -1));
    }

    [DataTestMethod]
    [DataRow(255, 0, 0, 0d, 100d, 50d)]
    [DataRow(0, 255, 0, 120d, 100d, 50d)]
    [DataRow(0, 0, 255, 240d, 100d, 50d)]
    [DataRow(128, 128, 128, 0d, 0d, 50.19607843137255d)]
    public void RgbToHsl_ConvertsPrimaryAndGrayColors(int red, int green, int blue, double hue, double saturation, double lightness)
    {
        HslColor hsl = ColorConversionService.ToHsl(new ColorValue(255, (byte)red, (byte)green, (byte)blue));
        Assert.AreEqual(hue, hsl.Hue, 0.0001d);
        Assert.AreEqual(saturation, hsl.Saturation, 0.0001d);
        Assert.AreEqual(lightness, hsl.Lightness, 0.0001d);
    }

    [TestMethod]
    public void HslToRgb_NormalizesHue360AndHonorsSaturationAndLightnessEdges()
    {
        Assert.AreEqual(ColorConversionService.FromHsl(0, 100, 50), ColorConversionService.FromHsl(360, 100, 50));
        Assert.AreEqual(new ColorValue(255, 128, 128, 128), ColorConversionService.FromHsl(240, 0, 50.19607843137255));
        Assert.AreEqual(new ColorValue(255, 0, 0, 0), ColorConversionService.FromHsl(123, 100, 0));
        Assert.AreEqual(new ColorValue(255, 255, 255, 255), ColorConversionService.FromHsl(123, 100, 100));
    }

    [TestMethod]
    public void HslToRgb_RejectsValuesOutsideDocumentedRanges()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromHsl(-1, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromHsl(361, 0, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromHsl(0, -1, 0));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromHsl(0, 0, 101));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ColorConversionService.FromHsl(double.NaN, 0, 0));
    }

    [DataTestMethod]
    [DataRow(0, 0, 0)]
    [DataRow(255, 255, 255)]
    [DataRow(74, 122, 211)]
    [DataRow(255, 0, 0)]
    public void RgbHslRoundTrip_StaysWithinOneByte(int red, int green, int blue)
    {
        ColorValue original = new(137, (byte)red, (byte)green, (byte)blue);
        HslColor hsl = ColorConversionService.ToHsl(original);
        ColorValue roundTrip = ColorConversionService.FromHsl(hsl.Hue, hsl.Saturation, hsl.Lightness, original.A);
        Assert.AreEqual(original.A, roundTrip.A);
        Assert.IsTrue(Math.Abs(original.R - roundTrip.R) <= 1);
        Assert.IsTrue(Math.Abs(original.G - roundTrip.G) <= 1);
        Assert.IsTrue(Math.Abs(original.B - roundTrip.B) <= 1);
    }

    [TestMethod]
    public void Hex8_IsAlwaysAarrggbbAndOpaqueHexStaysSixDigits()
    {
        Assert.AreEqual("#80FF0000", new ColorValue(128, 255, 0, 0).ToHex());
        Assert.AreEqual("#FF0000", new ColorValue(255, 255, 0, 0).ToHex());
        Assert.AreEqual(new ColorValue(0, 255, 0, 0), Parse("#00FF0000"));
        Assert.AreEqual(new ColorValue(255, 255, 0, 0), Parse("#FFFF0000"));
    }

    [TestMethod]
    public void ColorViewModel_ConvertsFormatsRejectsInvalidInputAndCopiesUnicodeText()
    {
        FakeClipboard clipboard = new();
        using ColorToolsViewModel viewModel = new(clipboard, new FakePicker());

        viewModel.HexText = "#00FF00";
        Assert.AreEqual("0", viewModel.RedText);
        Assert.AreEqual("255", viewModel.GreenText);
        Assert.AreEqual("120", viewModel.HueText);
        viewModel.RedText = "999";
        Assert.AreEqual((byte)0, viewModel.CurrentColor.R);
        Assert.IsTrue(viewModel.Error.Length > 0);
        viewModel.RedText = "255";
        Assert.AreEqual("#FFFF00", viewModel.HexText);
        viewModel.AlphaPercent = 50;
        Assert.AreEqual((byte)128, viewModel.CurrentColor.A);
        Assert.AreEqual("#80FFFF00", viewModel.HexText);

        viewModel.CopyRgbCommand.Execute(null);
        Assert.AreEqual("RGBA(255, 255, 0, 128)", clipboard.Text);
        viewModel.CopyHexCommand.Execute(null);
        Assert.AreEqual("#80FFFF00", clipboard.Text);
        viewModel.CopyHslCommand.Execute(null);
        Assert.AreEqual("HSLA(60, 100%, 50%, 50%)", clipboard.Text);
    }

    [TestMethod]
    public void RecentColors_MoveDuplicatesToFrontAndKeepOnlyTenInMemory()
    {
        using ColorToolsViewModel viewModel = new(new FakeClipboard(), new FakePicker());
        for (int index = 0; index < 12; index++)
            viewModel.HexText = $"#{index:X2}00AA";
        ColorValue duplicate = viewModel.RecentColors[5].Value;
        viewModel.SelectRecentCommand.Execute(viewModel.RecentColors[5]);

        Assert.AreEqual(ColorToolsViewModel.MaximumRecentColors, viewModel.RecentColors.Count);
        Assert.AreEqual(duplicate, viewModel.RecentColors[0].Value);
        Assert.AreEqual(1, viewModel.RecentColors.Count(item => item.Value == duplicate));
        using ColorToolsViewModel newSession = new(new FakeClipboard(), new FakePicker());
        Assert.AreEqual(0, newSession.RecentColors.Count);
    }

    [TestMethod]
    public async Task ScreenPicker_PreviewDoesNotCommitUntilConfirmed()
    {
        FakePicker picker = new()
        {
            Handler = (preview, _) =>
            {
                preview(Color.FromRgb(10, 20, 30));
                return Task.FromResult<Color?>(Color.FromRgb(10, 20, 30));
            }
        };
        using ColorToolsViewModel viewModel = new(new FakeClipboard(), picker);
        viewModel.StartPickerCommand.Execute(null);
        await WaitUntilAsync(() => !viewModel.IsPicking, TimeSpan.FromSeconds(1));

        Assert.AreEqual(new ColorValue(255, 10, 20, 30), viewModel.CurrentColor);
        Assert.AreEqual("#0A141E", viewModel.HexText);
        Assert.AreEqual("屏幕取色完成", viewModel.Status);
        Assert.AreEqual(0, picker.ActiveSessions);
    }

    [TestMethod]
    public async Task ScreenPicker_EscCancelCleansSessionAndKeepsCommittedColor()
    {
        FakePicker picker = new();
        using ColorToolsViewModel viewModel = new(new FakeClipboard(), picker);
        ColorValue before = viewModel.CurrentColor;
        viewModel.StartPickerCommand.Execute(null);
        await WaitUntilAsync(() => picker.ActiveSessions == 1, TimeSpan.FromSeconds(1));
        viewModel.CancelPickerCommand.Execute(null);
        await WaitUntilAsync(() => !viewModel.IsPicking, TimeSpan.FromSeconds(1));

        Assert.AreEqual(before, viewModel.CurrentColor);
        Assert.AreEqual(0, picker.ActiveSessions);
        Assert.AreEqual(1, picker.CleanupCount);
        Assert.AreEqual("取色已取消", viewModel.Status);
    }

    [TestMethod]
    public async Task ScreenPicker_RightClickNullResultKeepsCommittedColorAndReleasesFakeSession()
    {
        FakePicker picker = new()
        {
            Handler = (preview, _) =>
            {
                preview(Color.FromRgb(10, 20, 30));
                return Task.FromResult<Color?>(null);
            }
        };
        using ColorToolsViewModel viewModel = new(new FakeClipboard(), picker);
        ColorValue before = viewModel.CurrentColor;
        viewModel.StartPickerCommand.Execute(null);
        await WaitUntilAsync(() => !viewModel.IsPicking, TimeSpan.FromSeconds(1));

        Assert.AreEqual(before, viewModel.CurrentColor);
        Assert.AreEqual(0, picker.ActiveSessions);
        Assert.AreEqual(1, picker.CleanupCount);
        Assert.AreEqual("取色已取消", viewModel.Status);
    }

    [TestMethod]
    public async Task ScreenPicker_ExceptionAndApplicationExitReleaseFakeSession()
    {
        FakePicker failing = new() { Handler = (_, _) => throw new InvalidOperationException("controlled") };
        using (ColorToolsViewModel viewModel = new(new FakeClipboard(), failing))
        {
            viewModel.StartPickerCommand.Execute(null);
            await WaitUntilAsync(() => !viewModel.IsPicking, TimeSpan.FromSeconds(1));
            Assert.AreEqual("取色失败", viewModel.Status);
        }

        FakePicker active = new();
        ColorToolsViewModel exiting = new(new FakeClipboard(), active);
        exiting.StartPickerCommand.Execute(null);
        await WaitUntilAsync(() => active.ActiveSessions == 1, TimeSpan.FromSeconds(1));
        exiting.Dispose();
        await WaitUntilAsync(() => active.ActiveSessions == 0, TimeSpan.FromSeconds(1));
        Assert.AreEqual(1, active.CleanupCount);
    }

    [DataTestMethod]
    [DataRow(300, 400, 10, 20, 30)]
    [DataRow(-1920, 400, 40, 50, 60)]
    [DataRow(300, -1080, 70, 80, 90)]
    [DataRow(-1920, -1080, 100, 110, 120)]
    public void ScreenSamplerContract_PreservesPhysicalVirtualDesktopCoordinates(int x, int y, int red, int green, int blue)
    {
        ScreenPoint expectedPoint = new(x, y);
        Color expectedColor = Color.FromRgb((byte)red, (byte)green, (byte)blue);
        FakeSampler sampler = new(expectedPoint, expectedColor);

        Assert.AreEqual(expectedPoint, sampler.GetCursorPosition());
        Assert.AreEqual(expectedColor, sampler.Sample(expectedPoint));
        Assert.AreEqual(expectedPoint, sampler.LastSampledPoint);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => sampler.Sample(new ScreenPoint(0, 0)));
    }

    private static ColorValue Parse(string hex)
    {
        Assert.IsTrue(ColorConversionService.TryParseHex(hex, out ColorValue color));
        return color;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Timed out while waiting for picker state.");
            await Task.Delay(10);
        }
    }

    private sealed class FakeClipboard : IUtilitiesTextClipboardAdapter
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }

    private sealed class FakePicker : IScreenColorPicker
    {
        private readonly TaskCompletionSource<Color?> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Func<Action<Color>, CancellationToken, Task<Color?>>? Handler { get; init; }
        public int ActiveSessions { get; private set; }
        public int CleanupCount { get; private set; }

        public async Task<Color?> PickAsync(Action<Color> preview, CancellationToken cancellationToken)
        {
            ActiveSessions++;
            try
            {
                if (Handler is not null)
                    return await Handler(preview, cancellationToken);
                using CancellationTokenRegistration registration = cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
                return await _completion.Task;
            }
            finally
            {
                ActiveSessions--;
                CleanupCount++;
            }
        }
    }

    private sealed class FakeSampler(ScreenPoint cursor, Color sample) : IScreenColorSampler
    {
        public ScreenPoint? LastSampledPoint { get; private set; }
        public ScreenPoint GetCursorPosition() => cursor;
        public Color Sample(ScreenPoint point)
        {
            if (point != cursor)
                throw new ArgumentOutOfRangeException(nameof(point));
            LastSampledPoint = point;
            return sample;
        }
    }
}
