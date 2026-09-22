using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public interface IQuickLaunchStore
{
    string FilePath { get; }
    Task<QuickLaunchLoadResult> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IEnumerable<QuickLaunchItem> items, IEnumerable<QuickLaunchGroup> groups, CancellationToken cancellationToken = default);
}
