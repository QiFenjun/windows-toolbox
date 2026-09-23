using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.Utilities.QR.Models;
using WindowsToolbox.Modules.Utilities.QR.Services;
using WindowsToolbox.Modules.Utilities.QR.ViewModels;
using WindowsToolbox.Modules.Utilities.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class QrToolsViewModelTests
{
    [TestMethod]
    public async Task QrViewModel_DebouncesAndRejectsStaleGenerationResults()
    {
        using ManualResetEventSlim oldStarted = new();
        using ManualResetEventSlim releaseOld = new();
        BitmapSource oldImage = SolidBitmap(255, 0, 0);
        BitmapSource newImage = SolidBitmap(0, 255, 0);
        StubQrCodeService service = new()
        {
            GenerateHandler = (text, _) =>
            {
                if (text == "old")
                {
                    oldStarted.Set();
                    releaseOld.Wait(TimeSpan.FromSeconds(5));
                    return oldImage;
                }
                return newImage;
            }
        };
        using QrToolsViewModel viewModel = CreateViewModel(service);

        viewModel.InputText = "old";
        Assert.IsTrue(await Task.Run(() => oldStarted.Wait(TimeSpan.FromSeconds(3))));
        viewModel.InputText = "new";
        await WaitUntilAsync(() => ReferenceEquals(viewModel.PreviewImage, newImage), TimeSpan.FromSeconds(4));
        releaseOld.Set();
        await Task.Delay(100);

        Assert.AreSame(newImage, viewModel.PreviewImage);
        Assert.AreEqual("预览已更新 · 512 × 512", viewModel.Status);
    }

    [TestMethod]
    public async Task QrViewModel_GenerationFailureKeepsPreviousImage()
    {
        BitmapSource previousImage = SolidBitmap(20, 40, 60);
        StubQrCodeService service = new()
        {
            GenerateHandler = (text, _) => text == "bad" ? throw new InvalidOperationException() : previousImage
        };
        using QrToolsViewModel viewModel = CreateViewModel(service);

        viewModel.InputText = "good";
        await WaitUntilAsync(() => ReferenceEquals(viewModel.PreviewImage, previousImage), TimeSpan.FromSeconds(3));
        viewModel.InputText = "bad";
        await WaitUntilAsync(() => viewModel.Error.Length > 0, TimeSpan.FromSeconds(3));

        Assert.AreSame(previousImage, viewModel.PreviewImage);
        Assert.AreEqual("生成失败，保留上一次预览", viewModel.Status);
    }

    [TestMethod]
    public async Task QrViewModel_DecodeCancellationDoesNotPublishLateResult()
    {
        using ManualResetEventSlim decodeStarted = new();
        using ManualResetEventSlim releaseDecode = new();
        StubQrCodeService service = new()
        {
            DecodeHandler = (image, token) =>
            {
                decodeStarted.Set();
                releaseDecode.Wait(TimeSpan.FromSeconds(5));
                token.ThrowIfCancellationRequested();
                return QrDecodeResult.FromText("late result");
            }
        };
        using QrToolsViewModel viewModel = CreateViewModel(service);
        BitmapSource image = SolidBitmap(1, 2, 3);

        Task decoding = viewModel.DecodeImageAsync(image);
        Assert.IsTrue(await Task.Run(() => decodeStarted.Wait(TimeSpan.FromSeconds(3))));
        viewModel.CancelDecodeCommand.Execute(null);
        releaseDecode.Set();
        await decoding;

        Assert.IsFalse(viewModel.IsDecoding);
        Assert.IsNull(viewModel.DecodedResult);
        Assert.AreEqual("已取消二维码识别", viewModel.Status);
    }

    [TestMethod]
    public async Task QrViewModel_DecodeDropsStaleResultsAndCopiesUnicodeAsText()
    {
        using ManualResetEventSlim oldStarted = new();
        using ManualResetEventSlim releaseOld = new();
        BitmapSource oldImage = SolidBitmap(1, 1, 1);
        BitmapSource newImage = SolidBitmap(2, 2, 2);
        StubQrCodeService service = new()
        {
            DecodeHandler = (image, _) =>
            {
                if (ReferenceEquals(image, oldImage))
                {
                    oldStarted.Set();
                    releaseOld.Wait(TimeSpan.FromSeconds(5));
                    return QrDecodeResult.FromText("old");
                }
                return QrDecodeResult.FromText("你好 😀");
            }
        };
        FakeClipboard clipboard = new();
        using QrToolsViewModel viewModel = new(service, clipboard, clipboard);

        Task first = viewModel.DecodeImageAsync(oldImage);
        Assert.IsTrue(await Task.Run(() => oldStarted.Wait(TimeSpan.FromSeconds(3))));
        await viewModel.DecodeImageAsync(newImage);
        releaseOld.Set();
        await first;
        viewModel.CopyDecodedTextCommand.Execute(null);

        Assert.AreEqual("你好 😀", viewModel.DecodedResult!.Text);
        Assert.AreEqual("你好 😀", clipboard.Text);
    }

    [TestMethod]
    public async Task QrViewModel_RejectsMultipleDroppedFiles()
    {
        using QrToolsViewModel viewModel = CreateViewModel(new StubQrCodeService());

        await viewModel.DecodeDroppedFilesAsync(["one.png", "two.png"]);

        Assert.AreEqual("一次请选择一张图片。", viewModel.Error);
    }

    [TestMethod]
    public void QrImageFileLoader_RejectsCorruptAndUnsupportedImages()
    {
        string folder = CreateTestFolder();
        try
        {
            string corrupt = Path.Combine(folder, "bad.png");
            File.WriteAllBytes(corrupt, [1, 2, 3, 4]);
            Assert.ThrowsException<InvalidDataException>(() => QrImageFileLoader.Load(Path.Combine(folder, "bad.webp")));
            Assert.ThrowsException<FileNotFoundException>(() => QrImageFileLoader.Load(Path.Combine(folder, "missing.png")));
            try
            {
                _ = QrImageFileLoader.Load(corrupt);
                Assert.Fail("Corrupt image data should be rejected.");
            }
            catch (AssertFailedException) { throw; }
            catch (Exception) { }
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [TestMethod]
    public void QrImageFileLoader_DecodesPngJpegAndBmpWithoutKeepingSourceStream()
    {
        string folder = CreateTestFolder();
        QrCodeService qr = new();
        BitmapSource source = qr.Generate("image formats 中文", 512, QrErrorCorrection.Medium, QrQuietZoneStyle.Standard);
        try
        {
            AssertImageRoundTrip(folder, "qr.png", source, new PngBitmapEncoder(), qr);
            JpegBitmapEncoder jpeg = new() { QualityLevel = 92 };
            AssertImageRoundTrip(folder, "qr.jpg", source, jpeg, qr);
            AssertImageRoundTrip(folder, "qr.bmp", source, new BmpBitmapEncoder(), qr);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static void AssertImageRoundTrip(string folder, string filename, BitmapSource image, BitmapEncoder encoder, QrCodeService qr)
    {
        string path = Path.Combine(folder, filename);
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (FileStream output = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            encoder.Save(output);
        BitmapSource loaded = QrImageFileLoader.Load(path);
        Assert.IsTrue(loaded.IsFrozen);
        Assert.AreEqual("image formats 中文", qr.Decode(loaded)!.Text);
        File.Delete(path);
        Assert.IsFalse(File.Exists(path));
    }

    private static string CreateTestFolder() => Directory.CreateDirectory(Path.Combine(
        Path.GetTempPath(), "WindowsToolbox.Utilities.Tests", Guid.NewGuid().ToString("N"))).FullName;

    private static QrToolsViewModel CreateViewModel(StubQrCodeService service)
    {
        FakeClipboard clipboard = new();
        return new QrToolsViewModel(service, clipboard, clipboard);
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTime deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
                Assert.Fail("Timed out while waiting for QR view model state.");
            await Task.Delay(20);
        }
    }

    private static BitmapSource SolidBitmap(byte red, byte green, byte blue)
    {
        byte[] pixels = [blue, green, red, byte.MaxValue];
        BitmapSource image = BitmapSource.Create(1, 1, 96, 96, PixelFormats.Bgra32, null, pixels, 4);
        image.Freeze();
        return image;
    }

    private sealed class StubQrCodeService : IQrCodeService
    {
        public Func<string, int, BitmapSource>? GenerateHandler { get; init; }
        public Func<BitmapSource, CancellationToken, QrDecodeResult?>? DecodeHandler { get; init; }
        public BitmapSource Generate(string text, int size, QrErrorCorrection errorCorrection, QrQuietZoneStyle quietZoneStyle) =>
            GenerateHandler?.Invoke(text, size) ?? SolidBitmap(1, 2, 3);
        public QrDecodeResult? Decode(BitmapSource bitmapSource, CancellationToken cancellationToken = default) =>
            DecodeHandler?.Invoke(bitmapSource, cancellationToken);
        public void SavePng(BitmapSource bitmapSource, Stream output) { }
    }

    private sealed class FakeClipboard : IImageClipboardAdapter, IUtilitiesTextClipboardAdapter
    {
        public BitmapSource? Image { get; private set; }
        public string? Text { get; private set; }
        public BitmapSource? GetImage() => Image;
        public void SetImage(BitmapSource image) => Image = image;
        public void SetText(string text) => Text = text;
    }
}
