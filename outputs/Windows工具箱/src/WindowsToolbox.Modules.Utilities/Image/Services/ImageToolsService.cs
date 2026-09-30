using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.Utilities.Image.Models;

namespace WindowsToolbox.Modules.Utilities.Image.Services;

public static class ImageToolsService
{
    public const int MaximumDimension = 32768;
    public const long MaximumPixels = 40_000_000;
    public const long MaximumFileBytes = 128L * 1024 * 1024;
    public const int PreviewDimension = 1200;
    public const int MaximumBatch = 500;

    public static void ValidateSize(int width, int height)
    {
        if (width <= 0 || height <= 0 || width > MaximumDimension || height > MaximumDimension ||
            (long)width * height > MaximumPixels)
            throw new ArgumentOutOfRangeException(nameof(width), "尺寸必须为 1–32768 px，且总像素不超过 4000 万。");
    }

    public static (int Width, int Height) CalculateSize(int width, int height, ImageOptions options)
    {
        ValidateSize(width, height);
        if (!options.Resize) return (width, height);
        ValidateSize(options.Width, options.Height);
        if (!options.KeepAspect)
            return (options.NoUpscale ? Math.Min(width, options.Width) : options.Width,
                options.NoUpscale ? Math.Min(height, options.Height) : options.Height);
        double scale = Math.Min(options.Width / (double)width, options.Height / (double)height);
        if (options.NoUpscale) scale = Math.Min(1, scale);
        return (Math.Max(1, (int)Math.Round(width * scale, MidpointRounding.AwayFromZero)),
            Math.Max(1, (int)Math.Round(height * scale, MidpointRounding.AwayFromZero)));
    }

    public static ImageInfo ReadInfo(string path)
    {
        using FileStream stream = OpenSource(path);
        return Inspect(stream, path);
    }

    private static ImageInfo Inspect(FileStream stream, string path)
    {
        if (stream.Length > MaximumFileBytes)
            throw new InvalidDataException("图片不存在或文件超过 128 MiB。");
        BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        string format = decoder switch
        {
            PngBitmapDecoder => "PNG", JpegBitmapDecoder => "JPEG", BmpBitmapDecoder => "BMP",
            _ => throw new NotSupportedException("仅支持 PNG、JPEG 和 BMP 图片。")
        };
        BitmapFrame frame = decoder.Frames[0];
        ValidateSize(frame.PixelWidth, frame.PixelHeight);
        int orientation = 1;
        if (frame.Metadata is BitmapMetadata metadata)
        {
            object? value = metadata.GetQuery("/app1/ifd/{ushort=274}");
            if (value is ushort number && number is >= 1 and <= 8) orientation = number;
        }
        return new(System.IO.Path.GetFullPath(path), format, frame.PixelWidth, frame.PixelHeight,
            frame.DpiX, frame.DpiY, frame.Format.ToString(), stream.Length, decoder.Frames.Count, orientation);
    }

    public static BitmapSource LoadPreview(string path)
    {
        using FileStream stream = OpenSource(path);
        ImageInfo info = Inspect(stream, path);
        stream.Position = 0;
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        if (info.Width >= info.Height) image.DecodePixelWidth = Math.Min(info.Width, PreviewDimension);
        else image.DecodePixelHeight = Math.Min(info.Height, PreviewDimension);
        image.EndInit();
        image.Freeze();
        return ApplyOrientation(image, info.Orientation);
    }

    public static BitmapSource ApplyOrientation(BitmapSource source, int orientation)
    {
        if (orientation is < 1 or > 8) throw new ArgumentOutOfRangeException(nameof(orientation));
        if (orientation == 1) return source;
        int width = source.PixelWidth, height = source.PixelHeight;
        Matrix matrix = orientation switch
        {
            2 => new(-1, 0, 0, 1, width, 0),
            3 => new(-1, 0, 0, -1, width, height),
            4 => new(1, 0, 0, -1, 0, height),
            5 => new(0, 1, 1, 0, 0, 0),
            6 => new(0, 1, -1, 0, height, 0),
            7 => new(0, -1, -1, 0, height, width),
            _ => new(0, -1, 1, 0, 0, width)
        };
        BitmapSource result = new TransformedBitmap(source, new MatrixTransform(matrix));
        result.Freeze();
        return result;
    }

    public static string OutputPath(string source, ImageOptions options)
    {
        string extension = options.Format switch
        {
            ImageOutputFormat.Png => ".png", ImageOutputFormat.Jpeg => ".jpg", ImageOutputFormat.Bmp => ".bmp",
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        string directory = string.IsNullOrWhiteSpace(options.OutputDirectory)
            ? System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(source))! : options.OutputDirectory;
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(directory,
            System.IO.Path.GetFileNameWithoutExtension(source) + (options.Resize ? "_resized" : "_converted") + extension));
    }

    public static ImageResult Process(string path, ImageOptions options, CancellationToken token = default)
    {
        token.ThrowIfCancellationRequested();
        if (options.JpegQuality is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(options));
        string output = OutputPath(path, options);
        if (string.Equals(System.IO.Path.GetFullPath(path), output, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("输出不能覆盖源文件。");
        if (File.Exists(output)) return new(path, output, ImageItemStatus.Conflict, "目标已存在，已跳过；未覆盖。");
        ImageInfo info;
        BitmapSource source;
        using (FileStream stream = OpenSource(path))
        {
            info = Inspect(stream, path);
            stream.Position = 0;
            BitmapDecoder decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            source = decoder.Frames[0];
            source.Freeze();
        }
        token.ThrowIfCancellationRequested();
        source = ApplyOrientation(source, info.Orientation);
        (int width, int height) = CalculateSize(source.PixelWidth, source.PixelHeight, options);
        DrawingVisual visual = new();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext context = visual.RenderOpen())
        {
            if (options.Format != ImageOutputFormat.Png) context.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            context.DrawImage(source, new Rect(0, 0, width, height));
        }
        RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        BitmapEncoder encoder = options.Format switch
        {
            ImageOutputFormat.Png => new PngBitmapEncoder(),
            ImageOutputFormat.Jpeg => new JpegBitmapEncoder { QualityLevel = options.JpegQuality },
            ImageOutputFormat.Bmp => new BmpBitmapEncoder(),
            _ => throw new ArgumentOutOfRangeException(nameof(options))
        };
        // Re-encoding a rendered pixel surface deliberately strips source metadata, including GPS.
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        try { WriteAtomic(output, encoder.Save, token); }
        catch (IOException) when (File.Exists(output))
        { return new(path, output, ImageItemStatus.Conflict, "目标已存在，已跳过；未覆盖。"); }
        return new(path, output, ImageItemStatus.Succeeded, $"{width} × {height} px");
    }

    public static void WriteAtomic(string output, Action<Stream> encode, CancellationToken token = default)
    {
        string temporary = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(output))!, $".wt-image-{Guid.NewGuid():N}.tmp");
        try
        {
            using (FileStream stream = new(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                encode(stream);
                stream.Flush(true);
            }
            token.ThrowIfCancellationRequested();
            File.Move(temporary, output, overwrite: false);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static FileStream OpenSource(string path) => new(path, FileMode.Open, FileAccess.Read, FileShare.Read);

    public static Task<T> OnWorkerAsync<T>(Func<T> action)
    {
        TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Thread worker = new(() =>
        {
            try { completion.TrySetResult(action()); }
            catch (Exception ex) { completion.TrySetException(ex); }
        }) { IsBackground = true, Name = "Image Tools" };
        worker.SetApartmentState(ApartmentState.STA);
        worker.Start();
        return completion.Task;
    }

    public static Task<IReadOnlyList<ImageResult>> ProcessBatchAsync(IReadOnlyList<string> paths, ImageOptions options,
        IProgress<ImageResult>? progress = null, CancellationToken token = default)
    {
        if (paths.Count > MaximumBatch) throw new ArgumentOutOfRangeException(nameof(paths));
        string[] snapshot = paths.ToArray();
        return OnWorkerAsync<IReadOnlyList<ImageResult>>(() =>
        {
            List<ImageResult> results = [];
            HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
            // One STA worker processes the batch sequentially, bounding decoded-image memory.
            foreach (string path in snapshot)
            {
                ImageResult result;
                try
                {
                    if (token.IsCancellationRequested) result = new(path, null, ImageItemStatus.Cancelled);
                    else if (!seen.Add(System.IO.Path.GetFullPath(path))) result = new(path, null, ImageItemStatus.Skipped, "重复文件。");
                    else
                    {
                        progress?.Report(new(path, null, ImageItemStatus.Processing));
                        result = Process(path, options, token);
                    }
                }
                catch (OperationCanceledException) { result = new(path, null, ImageItemStatus.Cancelled); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException or NotSupportedException or
                    InvalidOperationException or System.Runtime.InteropServices.COMException or OutOfMemoryException)
                { result = new(path, null, ImageItemStatus.Failed, "无法处理图片，请检查格式、尺寸和输出目录权限。"); }
                results.Add(result);
                progress?.Report(result);
            }
            return results.AsReadOnly();
        });
    }
}
