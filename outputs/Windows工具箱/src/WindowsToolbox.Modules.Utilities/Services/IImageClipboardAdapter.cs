using System.Windows.Media.Imaging;

namespace WindowsToolbox.Modules.Utilities.Services;

public interface IImageClipboardAdapter
{
    BitmapSource? GetImage();
    void SetImage(BitmapSource image);
}
