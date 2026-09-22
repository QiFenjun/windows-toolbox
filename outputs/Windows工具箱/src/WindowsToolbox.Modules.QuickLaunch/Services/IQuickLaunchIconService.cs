using System.Windows.Media;
using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public interface IQuickLaunchIconService
{
    Task<ImageSource?> GetAsync(QuickLaunchItem item, int size = 40, CancellationToken cancellationToken = default);
}
