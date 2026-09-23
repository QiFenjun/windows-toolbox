using System.Windows;
using System.Windows.Media.Imaging;

namespace WindowsToolbox.Modules.Utilities.Services;

public sealed class WindowsImageClipboardAdapter : IImageClipboardAdapter
{
    public BitmapSource? GetImage() => Clipboard.ContainsImage() ? Clipboard.GetImage() : null;

    public void SetImage(BitmapSource image)
    {
        ArgumentNullException.ThrowIfNull(image);
        Clipboard.SetImage(image);
    }
}
