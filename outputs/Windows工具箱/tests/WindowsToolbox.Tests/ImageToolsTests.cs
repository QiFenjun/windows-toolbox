using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.Utilities.Image.Models;
using WindowsToolbox.Modules.Utilities.Image.Services;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class ImageToolsTests
{
    [TestMethod]
    public void ImageDescriptorIsInternal() => Assert.AreEqual("图片尺寸调整、格式转换与信息查看",
        UtilitiesViewModel.Tools.Single(tool => tool.Id == "image-tools").Description);

    [DataTestMethod]
    [DataRow(1920, 1080, 1000, 1000, true, true, 1000, 563)]
    [DataRow(1080, 1920, 1000, 1000, true, true, 563, 1000)]
    [DataRow(500, 500, 1920, 1080, true, true, 500, 500)]
    [DataRow(500, 500, 1920, 1080, true, false, 1080, 1080)]
    [DataRow(1920, 1080, 1000, 1000, false, true, 1000, 1000)]
    [DataRow(500, 500, 1000, 200, false, true, 500, 200)]
    public void ResizeDimensions(int w, int h, int targetW, int targetH, bool aspect, bool noUpscale, int expectedW, int expectedH) =>
        Assert.AreEqual((expectedW, expectedH), ImageToolsService.CalculateSize(w, h, new(targetW, targetH, aspect, noUpscale)));

    [DataTestMethod]
    [DataRow(0, 10)][DataRow(-1, 10)][DataRow(10, 0)][DataRow(10, -1)]
    [DataRow(32769, 10)][DataRow(10000, 10000)][DataRow(int.MaxValue, int.MaxValue)]
    public void InvalidDimensionsRejected(int width, int height) =>
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ImageToolsService.ValidateSize(width, height));

    [DataTestMethod]
    [DataRow("png")][DataRow("jpg")][DataRow("bmp")]
    public async Task MetadataPreviewAndUnlock(string format) => await InFixture(root =>
    {
        string path = CreateImage(root, format);
        ImageInfo info = ImageToolsService.ReadInfo(path);
        Assert.AreEqual(80, info.Width); Assert.AreEqual(40, info.Height);
        Assert.AreEqual(new FileInfo(path).Length, info.FileSize);
        Assert.AreEqual(96, info.DpiX, 1); Assert.AreEqual(96, info.DpiY, 1);
        Assert.AreEqual(1, info.FrameCount);
        BitmapSource preview = ImageToolsService.LoadPreview(path);
        Assert.IsTrue(preview.IsFrozen);
        string renamed = path + ".renamed";
        File.Move(path, renamed); File.Delete(renamed);
        Assert.IsFalse(File.Exists(renamed));
    });

    [TestMethod]
    public async Task LargePreviewIsBounded() => await InFixture(root =>
    {
        string path = CreateImage(root, "png", width: 2400, height: 1600);
        BitmapSource preview = ImageToolsService.LoadPreview(path);
        Assert.IsTrue(preview.PixelWidth <= 1200 && preview.PixelHeight <= 1200);
        Assert.AreEqual(2400, ImageToolsService.ReadInfo(path).Width);
    });

    [DataTestMethod]
    [DataRow("png", ImageOutputFormat.Jpeg)][DataRow("jpg", ImageOutputFormat.Png)]
    [DataRow("bmp", ImageOutputFormat.Png)][DataRow("png", ImageOutputFormat.Bmp)]
    public async Task RealResizeAndEncode(string input, ImageOutputFormat output) => await InFixture(root =>
    {
        string path = CreateImage(root, input);
        byte[] original = File.ReadAllBytes(path);
        ImageResult result = ImageToolsService.Process(path, new(20, 20, Format: output));
        Assert.AreEqual(ImageItemStatus.Succeeded, result.Status);
        ImageInfo info = ImageToolsService.ReadInfo(result.Output!);
        Assert.AreEqual(20, info.Width); Assert.AreEqual(10, info.Height);
        Assert.AreEqual(output == ImageOutputFormat.Jpeg ? "JPEG" : output.ToString().ToUpperInvariant(), info.Format);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(path));
        Assert.IsTrue(result.Output!.Contains("_resized"));
        Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
    });

    [DataTestMethod][DataRow(1)][DataRow(90)][DataRow(100)]
    public async Task JpegQualityAndWhiteAlpha(int quality) => await InFixture(root =>
    {
        string path = CreateImage(root, "png", transparent: true);
        ImageResult result = ImageToolsService.Process(path, new(Resize: false, Format: ImageOutputFormat.Jpeg, JpegQuality: quality));
        Assert.AreEqual(ImageItemStatus.Succeeded, result.Status);
        BitmapSource output = ImageToolsService.LoadPreview(result.Output!);
        BitmapSource bgra = new FormatConvertedBitmap(output, PixelFormats.Bgra32, null, 0);
        byte[] pixels = new byte[bgra.PixelWidth * bgra.PixelHeight * 4]; bgra.CopyPixels(pixels, bgra.PixelWidth * 4, 0);
        Assert.IsTrue(pixels[0] > 245 && pixels[1] > 245 && pixels[2] > 245);
        Assert.IsTrue(result.Output!.Contains("_converted"));
    });

    [DataTestMethod][DataRow(0)][DataRow(101)]
    public async Task InvalidQualityRejected(int quality) => await InFixture(root =>
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => ImageToolsService.Process(CreateImage(root, "png"), new(JpegQuality: quality))));

    [DataTestMethod]
    [DataRow(1, 0, 1, 2, 3, 4, 5)]
    [DataRow(2, 2, 1, 0, 5, 4, 3)]
    [DataRow(3, 5, 4, 3, 2, 1, 0)]
    [DataRow(4, 3, 4, 5, 0, 1, 2)]
    [DataRow(5, 0, 3, 1, 4, 2, 5)]
    [DataRow(6, 3, 0, 4, 1, 5, 2)]
    [DataRow(7, 5, 2, 4, 1, 3, 0)]
    [DataRow(8, 2, 5, 1, 4, 0, 3)]
    public async Task OrientationPreservesVisualPixelOrder(int orientation, int a, int b, int c, int d, int e, int f) =>
        await ImageToolsService.OnWorkerAsync(() =>
        {
            byte[] input = new byte[24];
            for (int i = 0; i < 6; i++) { input[i * 4] = (byte)i; input[i * 4 + 3] = 255; }
            BitmapSource source = BitmapSource.Create(3, 2, 96, 96, PixelFormats.Bgra32, null, input, 12);
            BitmapSource output = ImageToolsService.ApplyOrientation(source, orientation);
            byte[] pixels = new byte[24]; output.CopyPixels(pixels, output.PixelWidth * 4, 0);
            CollectionAssert.AreEqual(new[] { a, b, c, d, e, f }, Enumerable.Range(0, 6).Select(i => (int)pixels[i * 4]).ToArray());
            return true;
        });

    [DataTestMethod][DataRow(1)][DataRow(3)][DataRow(6)][DataRow(8)]
    public async Task ExifReadAppliedAndMetadataStripped(int orientation) => await InFixture(root =>
    {
        string path = CreateImage(root, "jpg", orientation: orientation);
        Assert.AreEqual(orientation, ImageToolsService.ReadInfo(path).Orientation);
        ImageResult result = ImageToolsService.Process(path, new(Resize: false));
        ImageInfo output = ImageToolsService.ReadInfo(result.Output!);
        Assert.AreEqual(orientation >= 5 ? 40 : 80, output.Width);
        Assert.AreEqual(orientation >= 5 ? 80 : 40, output.Height);
        Assert.AreEqual(1, output.Orientation);
        using FileStream stream = File.OpenRead(result.Output!);
        BitmapMetadata? metadata = BitmapDecoder.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0].Metadata as BitmapMetadata;
        Assert.IsTrue(metadata is null || metadata.Comment != "private test metadata");
    });

    [TestMethod]
    public async Task AtomicFailureConflictAndCancellationLeaveTargetsIntact() => await InFixture(root =>
    {
        string output = Path.Combine(root, "existing.png"); File.WriteAllText(output, "original");
        Assert.ThrowsException<IOException>(() => ImageToolsService.WriteAtomic(output, stream => { stream.WriteByte(42); throw new IOException("test"); }));
        Assert.AreEqual("original", File.ReadAllText(output));
        Assert.ThrowsException<IOException>(() => ImageToolsService.WriteAtomic(output, stream => stream.WriteByte(42)));
        Assert.AreEqual("original", File.ReadAllText(output));
        using CancellationTokenSource cts = new(); cts.Cancel();
        Assert.ThrowsException<OperationCanceledException>(() => ImageToolsService.WriteAtomic(Path.Combine(root, "cancel.png"), stream => stream.WriteByte(42), cts.Token));
        Assert.AreEqual(0, Directory.GetFiles(root, "*.tmp").Length);
        string source = CreateImage(root, "png");
        ImageResult first = ImageToolsService.Process(source, new());
        byte[] original = File.ReadAllBytes(first.Output!);
        Assert.AreEqual(ImageItemStatus.Conflict, ImageToolsService.Process(source, new()).Status);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(first.Output!));
    });

    [TestMethod]
    public async Task BatchIsSequentialSkipsDuplicatesAndContinuesFailures()
    {
        string root = FixtureRoot();
        try
        {
            string path = await ImageToolsService.OnWorkerAsync(() => CreateImage(root, "png"));
            List<ImageResult> events = [];
            var results = await ImageToolsService.ProcessBatchAsync([path, path, Path.Combine(root, "absent.png")], new(), new InlineProgress(events.Add));
            CollectionAssert.AreEqual(new[] { ImageItemStatus.Succeeded, ImageItemStatus.Skipped, ImageItemStatus.Failed }, results.Select(r => r.Status).ToArray());
            Assert.AreEqual(ImageItemStatus.Processing, events[0].Status);
            Assert.AreEqual(ImageItemStatus.Succeeded, events[1].Status);
            Assert.AreEqual(ImageItemStatus.Processing, events[3].Status);
        }
        finally { Directory.Delete(root, true); }
    }

    [TestMethod]
    public async Task BatchCancellationStopsSubsequentItems()
    {
        string root = FixtureRoot();
        try
        {
            string path = await ImageToolsService.OnWorkerAsync(() => CreateImage(root, "png"));
            using CancellationTokenSource cts = new();
            var results = await ImageToolsService.ProcessBatchAsync([path, path + "not-started"], new(),
                new InlineProgress(result => { if (result.Status == ImageItemStatus.Succeeded) cts.Cancel(); }), cts.Token);
            Assert.AreEqual(ImageItemStatus.Succeeded, results[0].Status);
            Assert.AreEqual(ImageItemStatus.Cancelled, results[1].Status);
        }
        finally { Directory.Delete(root, true); }
    }

    private sealed class InlineProgress(Action<ImageResult> report) : IProgress<ImageResult> { public void Report(ImageResult value) => report(value); }
    internal static string FixtureRoot() => Directory.CreateDirectory(Path.Combine(Path.GetTempPath(), "WindowsToolbox.ImageTools.Tests", Guid.NewGuid().ToString("N"))).FullName;
    internal static async Task InFixture(Action<string> test)
    {
        string root = FixtureRoot();
        try { await ImageToolsService.OnWorkerAsync(() => { test(root); return true; }); }
        finally { Directory.Delete(root, true); }
    }

    internal static string CreateImage(string root, string format, bool transparent = false, int orientation = 1, int width = 80, int height = 40)
    {
        byte[] pixels = new byte[width * height * 4];
        for (int i = 0; i < width * height; i++) { pixels[i * 4 + 2] = 255; pixels[i * 4 + 3] = transparent ? (byte)0 : (byte)255; }
        BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, width * 4);
        BitmapEncoder encoder = format switch { "jpg" => new JpegBitmapEncoder(), "bmp" => new BmpBitmapEncoder(), _ => new PngBitmapEncoder() };
        BitmapMetadata? metadata = null;
        if (format == "jpg") { metadata = new("jpg"); metadata.SetQuery("/app1/ifd/{ushort=274}", (ushort)orientation); metadata.Comment = "private test metadata"; }
        encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
        string path = Path.Combine(root, $"测试-{Guid.NewGuid():N}.{format}");
        using FileStream stream = File.Create(path); encoder.Save(stream); return path;
    }
}
