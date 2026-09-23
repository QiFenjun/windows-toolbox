using MediaColor = System.Windows.Media.Color;
using WindowsToolbox.Modules.Utilities.Color.Models;

namespace WindowsToolbox.Modules.Utilities.Color.Services;

public interface IScreenColorSampler
{
    ScreenPoint GetCursorPosition();
    MediaColor Sample(ScreenPoint point);
}
