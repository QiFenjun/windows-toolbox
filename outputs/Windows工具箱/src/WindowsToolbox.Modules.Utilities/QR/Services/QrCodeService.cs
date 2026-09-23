using System.IO;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.Utilities.QR.Models;
using ZXing;
using ZXing.Common;
using ZXing.QrCode;
using ZXing.QrCode.Internal;

namespace WindowsToolbox.Modules.Utilities.QR.Services;

public sealed class QrCodeService : IQrCodeService
{
    public const int MinimumSize = 128;
    public const int MaximumSize = 2048;
    public const int MaximumInputBytes = 8192;

    public BitmapSource Generate(
        string text,
        int size,
        QrErrorCorrection errorCorrection,
        QrQuietZoneStyle quietZoneStyle)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("请输入要生成二维码的内容。", nameof(text));
        if (size is < MinimumSize or > MaximumSize)
            throw new ArgumentOutOfRangeException(nameof(size), $"二维码边长必须为 {MinimumSize} 至 {MaximumSize} 像素。");
        if (!Enum.IsDefined(errorCorrection))
            throw new ArgumentOutOfRangeException(nameof(errorCorrection));
        if (!Enum.IsDefined(quietZoneStyle))
            throw new ArgumentOutOfRangeException(nameof(quietZoneStyle));
        if (Encoding.UTF8.GetByteCount(text) > MaximumInputBytes)
            throw new ArgumentException("内容过长，请缩短后再生成二维码。", nameof(text));

        Dictionary<EncodeHintType, object> hints = new()
        {
            [EncodeHintType.CHARACTER_SET] = Encoding.UTF8.WebName,
            [EncodeHintType.ERROR_CORRECTION] = errorCorrection switch
            {
                QrErrorCorrection.Low => ErrorCorrectionLevel.L,
                QrErrorCorrection.Medium => ErrorCorrectionLevel.M,
                QrErrorCorrection.Quartile => ErrorCorrectionLevel.Q,
                QrErrorCorrection.High => ErrorCorrectionLevel.H,
                _ => throw new ArgumentOutOfRangeException(nameof(errorCorrection))
            },
            [EncodeHintType.MARGIN] = quietZoneStyle == QrQuietZoneStyle.Standard ? 4 : 2
        };

        BitMatrix matrix = new QRCodeWriter().encode(text, BarcodeFormat.QR_CODE, size, size, hints);
        byte[] pixels = new byte[checked(size * size * 4)];
        for (int y = 0; y < size; y++)
        {
            int rowStart = y * size * 4;
            for (int x = 0; x < size; x++)
            {
                byte channel = matrix[x, y] ? (byte)0 : byte.MaxValue;
                int pixel = rowStart + x * 4;
                pixels[pixel] = channel;
                pixels[pixel + 1] = channel;
                pixels[pixel + 2] = channel;
                pixels[pixel + 3] = byte.MaxValue;
            }
        }

        BitmapSource image = BitmapSource.Create(
            size, size, 96, 96, PixelFormats.Bgra32, null, pixels, size * 4);
        image.Freeze();
        return image;
    }

    public QrDecodeResult? Decode(BitmapSource bitmapSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(bitmapSource);
        cancellationToken.ThrowIfCancellationRequested();
        if (bitmapSource.PixelWidth <= 0 || bitmapSource.PixelHeight <= 0)
            return null;

        double scale = Math.Min(1d, QrImageFileLoader.MaximumDimension /
            (double)Math.Max(bitmapSource.PixelWidth, bitmapSource.PixelHeight));
        if (scale < 1d)
        {
            TransformedBitmap resized = new(bitmapSource, new ScaleTransform(scale, scale));
            resized.Freeze();
            bitmapSource = resized;
        }

        FormatConvertedBitmap converted = new(bitmapSource, PixelFormats.Bgra32, null, 0);
        if (!converted.IsFrozen)
            converted.Freeze();
        int width = converted.PixelWidth;
        int height = converted.PixelHeight;
        byte[] pixels = new byte[checked(width * height * 4)];
        converted.CopyPixels(pixels, width * 4, 0);
        cancellationToken.ThrowIfCancellationRequested();

        RGBLuminanceSource luminance = new(
            pixels, width, height, RGBLuminanceSource.BitmapFormat.BGRA32);
        BinaryBitmap binary = new(new HybridBinarizer(luminance));
        try
        {
            Result? result = new QRCodeReader().decode(binary);
            cancellationToken.ThrowIfCancellationRequested();
            return result is null ? null : QrDecodeResult.FromText(result.Text);
        }
        catch (ReaderException)
        {
            return null;
        }
    }

    public void SavePng(BitmapSource bitmapSource, Stream output)
    {
        ArgumentNullException.ThrowIfNull(bitmapSource);
        ArgumentNullException.ThrowIfNull(output);
        PngBitmapEncoder encoder = new();
        encoder.Frames.Add(BitmapFrame.Create(bitmapSource));
        encoder.Save(output);
    }
}
