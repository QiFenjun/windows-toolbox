namespace WindowsToolbox.Modules.Utilities.Image.Models;

public enum ImageOutputFormat { Png, Jpeg, Bmp }
public enum ImageItemStatus { Pending, Processing, Succeeded, Skipped, Conflict, Failed, Cancelled }

public sealed record ImageInfo(string Path, string Format, int Width, int Height, double DpiX,
    double DpiY, string PixelFormat, long FileSize, int FrameCount, int Orientation)
{
    public string FileName => System.IO.Path.GetFileName(Path);
    public int VisualWidth => Orientation >= 5 ? Height : Width;
    public int VisualHeight => Orientation >= 5 ? Width : Height;
    public string Summary => $"{FileName} · {Format} · {Width} × {Height} px · 显示比例 {(double)VisualWidth / VisualHeight:0.###}:1\n" +
        $"DPI {DpiX:0.##} × {DpiY:0.##} · {PixelFormat} · {FileSize:N0} bytes · {FrameCount} frame(s) · Orientation {Orientation}";
}

public sealed record ImageOptions(int Width = 1920, int Height = 1080, bool KeepAspect = true,
    bool NoUpscale = true, bool Resize = true, ImageOutputFormat Format = ImageOutputFormat.Png,
    int JpegQuality = 90, string? OutputDirectory = null);

public sealed record ImageResult(string Source, string? Output, ImageItemStatus Status, string Message = "");
