using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Core.Services;
using WindowsToolbox.Modules.Utilities;
using WindowsToolbox.Modules.Utilities.Models;
using WindowsToolbox.Modules.Utilities.QR.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class UtilitiesQrCodeTests
{
    private readonly QrCodeService _service = new();

    [TestMethod]
    public void UtilitiesModule_UsesOneBilingualTopLevelEntry()
    {
        ModuleRegistry registry = new();
        UtilitiesModule module = new();
        registry.Register(module);

        Assert.AreEqual("utilities", module.Id);
        Assert.AreEqual("小工具", module.DisplayName);
        Assert.AreEqual("Utilities", module.EnglishName);
        Assert.AreEqual("效率工具", module.Category);
        Assert.AreEqual("Productivity Tools", module.EnglishCategory);
        Assert.AreEqual(1, registry.Modules.Count);
        Assert.IsTrue(module.Keywords.Contains("QR"));
        Assert.IsTrue(module.Keywords.Contains("颜色"));
        Assert.IsTrue(module.Keywords.Contains("Time"));
        Assert.IsTrue(module.Keywords.Contains("Random"));
    }

    [TestMethod]
    public void UtilitiesNavigation_ContainsQrColorAndTimeToolsInOneModule()
    {
        CollectionAssert.AreEqual(new[] { "qr", "color", "time-tools", "random-tools" },
            UtilitiesViewModel.Tools.Select(tool => tool.Id).ToArray());

        UtilitiesViewModel viewModel = new();
        viewModel.SelectToolCommand.Execute("color");
        Assert.AreEqual("color", viewModel.SelectedToolId);
        Assert.AreEqual("Color Tools", viewModel.SelectedTool.EnglishName);
        viewModel.SelectToolCommand.Execute("time-tools");
        Assert.AreEqual("time-tools", viewModel.SelectedTool.Id);
        Assert.AreEqual("Time Tools", viewModel.SelectedTool.EnglishName);
        Assert.AreEqual("时间戳、时区与日期时间快速转换", viewModel.SelectedTool.Description);
        viewModel.SelectToolCommand.Execute("random-tools");
        Assert.AreEqual("random-tools", viewModel.SelectedTool.Id);
        Assert.AreEqual("随机工具", viewModel.SelectedTool.ChineseName);
        Assert.AreEqual("Random Tools", viewModel.SelectedTool.EnglishName);
        Assert.IsInstanceOfType(viewModel.SelectedToolContent, typeof(WindowsToolbox.Modules.Utilities.Random.ViewModels.RandomToolsViewModel));
        WindowsToolbox.Core.Services.ModuleRegistry registry = new();
        registry.Register(new UtilitiesModule());
        Assert.AreEqual(1, registry.Modules.Count);
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => viewModel.SelectedToolId = "qr-tools");
    }

    [DataTestMethod]
    [DataRow("Hello, QR", "Hello, QR")]
    [DataRow("中文二维码", "中文二维码")]
    [DataRow("日本語のQRコード", "日本語のQRコード")]
    [DataRow("emoji 😀 🎨", "emoji 😀 🎨")]
    [DataRow("line one\n第二行", "line one\n第二行")]
    [DataRow("https://example.com/path?q=1", "https://example.com/path?q=1")]
    public void QrCode_RoundTripsUtf8Text(string input, string expected)
    {
        BitmapSource image = _service.Generate(input, 512, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);

        Assert.IsTrue(image.IsFrozen);
        QrDecodeResult result = _service.Decode(image)!;
        Assert.IsNotNull(result);
        Assert.AreEqual(expected, result.Text);
        Assert.AreEqual(input.Length, result.CharacterCount);
    }

    [DataTestMethod]
    [DataRow(QrErrorCorrection.Low)]
    [DataRow(QrErrorCorrection.Medium)]
    [DataRow(QrErrorCorrection.Quartile)]
    [DataRow(QrErrorCorrection.High)]
    public void QrCode_AcceptsEveryErrorCorrectionLevel(QrErrorCorrection level)
    {
        BitmapSource image = _service.Generate("level", 256, level, QrQuietZoneStyle.Standard);
        Assert.AreEqual("level", _service.Decode(image)!.Text);
    }

    [DataTestMethod]
    [DataRow(128)]
    [DataRow(512)]
    [DataRow(1024)]
    [DataRow(2048)]
    public void QrCode_AcceptsBoundedSizes(int size)
    {
        BitmapSource image = _service.Generate("size", size, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
        Assert.AreEqual(size, image.PixelWidth);
        Assert.AreEqual(size, image.PixelHeight);
    }

    [TestMethod]
    public void QrCode_RejectsInvalidSizeAndInput()
    {
        Assert.ThrowsException<ArgumentException>(() => _service.Generate(" \n ", 512, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", 0, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", -1, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", 127, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", 2049, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentException>(() => _service.Generate(new string('x', QrCodeService.MaximumInputBytes + 1), 512, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard));
    }

    [TestMethod]
    public void QrCode_RejectsUndefinedOptions()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", 512, (QrErrorCorrection)99, QrQuietZoneStyle.Standard));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => _service.Generate("x", 512, QrErrorCorrection.Medium, (QrQuietZoneStyle)99));
    }

    [DataTestMethod]
    [DataRow(QrQuietZoneStyle.Standard)]
    [DataRow(QrQuietZoneStyle.Compact)]
    public void QrCode_KeepsQuietZoneAndDecodes(QrQuietZoneStyle style)
    {
        BitmapSource image = _service.Generate("quiet zone", 384, QrErrorCorrection.Medium, style);
        Assert.AreEqual("quiet zone", _service.Decode(image)!.Text);
    }

    [TestMethod]
    public void QrCode_ReturnsNoResultForImageWithoutQr()
    {
        BitmapSource white = SolidBitmap(64, 64, 255, 255, 255);

        Assert.IsNull(_service.Decode(white));
    }

    [TestMethod]
    public void QrDecodeResult_ClassifiesOnlyHttpAndHttpsSchemes()
    {
        Assert.AreEqual(QrContentKind.HttpUrl, QrDecodeResult.FromText("http://example.com").Kind);
        Assert.AreEqual(QrContentKind.HttpsUrl, QrDecodeResult.FromText("https://example.com").Kind);
        Assert.AreEqual(QrContentKind.Text, QrDecodeResult.FromText("mailto:user@example.com").Kind);
        Assert.AreEqual(QrContentKind.Text, QrDecodeResult.FromText("plain text").Kind);
    }

    [TestMethod]
    public void QrCode_HonorsCancellationBeforeDecode()
    {
        BitmapSource image = _service.Generate("cancel", 256, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.ThrowsException<OperationCanceledException>(() => _service.Decode(image, cancellation.Token));
    }

    [TestMethod]
    public void QrCode_ExportsPngIntoCallerOwnedStream()
    {
        string folder = Directory.CreateDirectory(Path.Combine(
            Path.GetTempPath(), "WindowsToolbox.Utilities.Tests", Guid.NewGuid().ToString("N"))).FullName;
        string path = Path.Combine(folder, "generated.png");
        try
        {
            BitmapSource image = _service.Generate("PNG", 256, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
            using (FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                _service.SavePng(image, output);
            using FileStream input = File.OpenRead(path);
            BitmapFrame frame = BitmapDecoder.Create(input, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
            Assert.AreEqual(256, frame.PixelWidth);
            Assert.AreEqual(256, frame.PixelHeight);
        }
        finally
        {
            if (Directory.Exists(folder))
                Directory.Delete(folder, recursive: true);
        }
    }

    [TestMethod]
    public void ImageClipboardAdapterContract_CanBeTestedWithoutRealClipboard()
    {
        FakeImageClipboardAdapter clipboard = new();
        BitmapSource image = _service.Generate("clipboard", 256, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
        clipboard.SetImage(image);

        Assert.AreSame(image, clipboard.GetImage());
        Assert.AreEqual(1, clipboard.WriteCount);
    }

    private static BitmapSource SolidBitmap(int width, int height, byte red, byte green, byte blue)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int index = 0; index < width * height; index++)
        {
            pixels[index * 4] = blue;
            pixels[index * 4 + 1] = green;
            pixels[index * 4 + 2] = red;
            pixels[index * 4 + 3] = byte.MaxValue;
        }
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        return bitmap;
    }

    private sealed class FakeImageClipboardAdapter : IImageClipboardAdapter
    {
        private BitmapSource? _image;
        public int WriteCount { get; private set; }
        public BitmapSource? GetImage() => _image;
        public void SetImage(BitmapSource image) { _image = image; WriteCount++; }
    }
}
