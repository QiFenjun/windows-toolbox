using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public static class InstalledAppDeduplicator
{
    public static IReadOnlyList<InstalledApplication> Merge(
        IEnumerable<InstalledApplication> applications)
    {
        ArgumentNullException.ThrowIfNull(applications);

        return applications
            .GroupBy(CreateKey, StringComparer.OrdinalIgnoreCase)
            .Select(MergeGroup)
            .OrderBy(item => item.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToArray();
    }

    private static string CreateKey(InstalledApplication app) =>
        string.Join(
            "\u001f",
            Normalize(app.DisplayName),
            Normalize(app.DisplayVersion),
            Normalize(app.Publisher),
            Normalize(app.UninstallString));

    private static InstalledApplication MergeGroup(
        IGrouping<string, InstalledApplication> group)
    {
        InstalledApplication preferred = group
            .OrderByDescending(app => !string.IsNullOrWhiteSpace(app.InstallLocation))
            .ThenByDescending(app => app.ReportedSizeBytes.HasValue)
            .ThenByDescending(app => app.CanUninstall)
            .First();

        return new InstalledApplication
        {
            Id = preferred.Id,
            DisplayName = preferred.DisplayName,
            DisplayVersion = FirstNonEmpty(group.Select(app => app.DisplayVersion)),
            Publisher = FirstNonEmpty(group.Select(app => app.Publisher)),
            InstallDate = group.Where(app => app.InstallDate.HasValue)
                .Select(app => app.InstallDate)
                .OrderByDescending(value => value)
                .FirstOrDefault(),
            InstallLocation = FirstNonEmpty(group.Select(app => app.InstallLocation)),
            DisplayIconPath = FirstNonEmpty(group.Select(app => app.DisplayIconPath)),
            ReportedSizeBytes = group.Max(app => app.ReportedSizeBytes),
            UninstallString = FirstNonEmpty(group.Select(app => app.UninstallString)),
            QuietUninstallString = FirstNonEmpty(group.Select(app => app.QuietUninstallString)),
            ModifyPath = FirstNonEmpty(group.Select(app => app.ModifyPath)),
            IsSystemComponent = group.Any(app => app.IsSystemComponent),
            IsSystemEntry = group.Any(app => app.IsSystemEntry),
            CanUninstall = group.Any(app => app.CanUninstall),
            RequiresElevation = group.Any(app => app.RequiresElevation),
            IsWindowsInstaller = group.Any(app => app.IsWindowsInstaller),
            NoRemove = group.All(app => app.NoRemove),
            ReleaseType = FirstNonEmpty(group.Select(app => app.ReleaseType)),
            ParentKeyName = FirstNonEmpty(group.Select(app => app.ParentKeyName)),
            Architecture = string.Join(
                " / ",
                group.Select(app => app.Architecture)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
            PackageId = FirstNonEmpty(group.Select(app => app.PackageId)),
            Source = preferred.Source,
            RegistryPath = string.Join(
                Environment.NewLine,
                group.Select(app => app.RegistryPath)
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.OrdinalIgnoreCase))
        };
    }

    private static string FirstNonEmpty(IEnumerable<string> values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();
}
