using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public static class InstalledAppQuery
{
    public static IReadOnlyList<InstalledApplication> Apply(
        IEnumerable<InstalledApplication> applications,
        string searchText,
        string sortMode,
        string publisher,
        string source,
        bool includeSystemComponents)
    {
        IEnumerable<InstalledApplication> query =
            InstalledAppService.FilterSystemComponents(applications, includeSystemComponents);

        string search = searchText.Trim();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(app =>
                app.DisplayName.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                app.DisplayVersion.Contains(search, StringComparison.CurrentCultureIgnoreCase) ||
                app.Publisher.Contains(search, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.Equals(publisher, "全部发布者", StringComparison.Ordinal))
        {
            query = query.Where(app =>
                string.Equals(app.PublisherText, publisher, StringComparison.CurrentCultureIgnoreCase));
        }

        if (!string.Equals(source, "全部来源", StringComparison.Ordinal))
        {
            query = query.Where(app =>
                string.Equals(app.SourceText, source, StringComparison.Ordinal));
        }

        return sortMode switch
        {
            "大小（从大到小）" => query
                .OrderByDescending(app => app.DisplaySizeBytes ?? -1)
                .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            "安装日期（从新到旧）" => query
                .OrderByDescending(app => app.InstallDate ?? DateTime.MinValue)
                .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            "发布者" => query
                .OrderBy(app => app.PublisherText, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray(),
            _ => query
                .OrderBy(app => app.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray()
        };
    }
}
