using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public interface IInstalledAppProvider
{
    Task<IReadOnlyList<InstalledApplication>> GetInstalledApplicationsAsync(
        CancellationToken cancellationToken);
}
