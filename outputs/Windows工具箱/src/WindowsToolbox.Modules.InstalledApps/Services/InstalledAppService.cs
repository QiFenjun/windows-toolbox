using System.IO;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class InstalledAppService(
    IEnumerable<IInstalledAppProvider> providers)
{
    private readonly IInstalledAppProvider[] _providers =
        providers?.ToArray() ?? throw new ArgumentNullException(nameof(providers));

    public async Task<IReadOnlyList<InstalledApplication>> LoadAsync(
        CancellationToken cancellationToken)
    {
        List<InstalledApplication> applications = [];
        foreach (IInstalledAppProvider provider in _providers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                IReadOnlyList<InstalledApplication> providerItems =
                    await provider.GetInstalledApplicationsAsync(cancellationToken);
                applications.AddRange(providerItems);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is IOException ||
                exception is InvalidOperationException)
            {
                // 一个数据源失败时保留其他来源的结果。
            }
        }

        return InstalledAppDeduplicator.Merge(applications);
    }

    public static IReadOnlyList<InstalledApplication> FilterSystemComponents(
        IEnumerable<InstalledApplication> applications,
        bool includeSystemComponents) =>
        applications
            .Where(app => includeSystemComponents || (!app.IsSystemComponent && !app.IsSystemEntry))
            .ToArray();
}
