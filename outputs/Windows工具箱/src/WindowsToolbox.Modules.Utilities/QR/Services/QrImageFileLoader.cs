using System.IO;
using System.Windows.Media.Imaging;

namespace WindowsToolbox.Modules.Utilities.QR.Services;

public static class QrImageFileLoader
{
    public const int MaximumDimension = 3000;
    public const long MaximumFileBytes = 64L * 1024 * 1024;

    public static BitmapSource Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string extension = Path.GetExtension(path);
        if (!new[] { ".png", ".jpg", ".jpeg", ".bmp" }.Contains(extension, StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("仅支持 PNG、JPG/JPEG 和 BMP 图片。");
        FileInfo file = new(path);
        if (!file.Exists)
            throw new FileNotFoundException("找不到所选图片。", path);
        if (file.Length > MaximumFileBytes)
            throw new InvalidDataException("图片文件过大，无法安全读取。");

        using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        BitmapDecoder metadata = BitmapDecoder.Create(
            stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.OnDemand);
        if (metadata.Frames.Count == 0)
            throw new InvalidDataException("图片中没有可读取的图像。");
        int sourceWidth = metadata.Frames[0].PixelWidth;
        int sourceHeight = metadata.Frames[0].PixelHeight;
        if (sourceWidth <= 0 || sourceHeight <= 0)
            throw new InvalidDataException("图片尺寸无效。");

        double scale = Math.Min(1d, MaximumDimension / (double)Math.Max(sourceWidth, sourceHeight));
        int width = Math.Max(1, (int)Math.Round(sourceWidth * scale));
        int height = Math.Max(1, (int)Math.Round(sourceHeight * scale));
        stream.Position = 0;
        BitmapImage image = new();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = width;
        image.DecodePixelHeight = height;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
