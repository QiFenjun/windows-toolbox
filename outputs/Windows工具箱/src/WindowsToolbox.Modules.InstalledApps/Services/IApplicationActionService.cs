using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public interface IApplicationActionService
{
    bool OpenInstallLocation(InstalledApplication application, out string errorMessage);
    Task<UninstallResult> UninstallAsync(
        InstalledApplication application,
        CancellationToken cancellationToken);
}

public interface IApplicationClipboardService
{
    void CopyText(string text);
}
