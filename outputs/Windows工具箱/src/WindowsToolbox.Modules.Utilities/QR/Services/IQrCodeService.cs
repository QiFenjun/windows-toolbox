using System.IO;
using System.Windows.Media.Imaging;
using WindowsToolbox.Modules.Utilities.QR.Models;

namespace WindowsToolbox.Modules.Utilities.QR.Services;

public interface IQrCodeService
{
    BitmapSource Generate(
        string text,
        int size,
        QrErrorCorrection errorCorrection,
        QrQuietZoneStyle quietZoneStyle);

    QrDecodeResult? Decode(BitmapSource bitmapSource, CancellationToken cancellationToken = default);

    void SavePng(BitmapSource bitmapSource, Stream output);
}
