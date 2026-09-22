using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public interface IQuickLaunchExecutor
{
    Task<QuickLaunchLaunchResult> LaunchAsync(QuickLaunchItem item, CancellationToken cancellationToken = default);
    Task<QuickLaunchLaunchResult> OpenLocationAsync(QuickLaunchItem item, CancellationToken cancellationToken = default);
}
