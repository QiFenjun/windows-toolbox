using MediaColor = System.Windows.Media.Color;

namespace WindowsToolbox.Modules.Utilities.Color.Services;

public interface IScreenColorPicker
{
    Task<MediaColor?> PickAsync(Action<MediaColor> preview, CancellationToken cancellationToken);
}
