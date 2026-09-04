using System.Windows.Media;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public interface IApplicationIconService
{
    Task<ImageSource?> GetIconAsync(
        InstalledApplication application,
        int desiredSize,
        CancellationToken cancellationToken);

    Task<ApplicationIconResult> GetIconResultAsync(
        InstalledApplication application,
        int desiredSize,
        CancellationToken cancellationToken);
}
