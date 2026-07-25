using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public static class RegistryEntryMapper
{
    public static InstalledApplication? Map(
        IReadOnlyDictionary<string, object?> values,
        string registryPath,
        string architecture,
        bool requiresElevation)
    {
        string name = GetString(values, "DisplayName");
        if (string.IsNullOrWhiteSpace(name))
            return null;

        string version = GetString(values, "DisplayVersion");
        string publisher = GetString(values, "Publisher");
        string uninstall = GetString(values, "UninstallString");
        string quietUninstall = GetString(values, "QuietUninstallString");
        string releaseType = GetString(values, "ReleaseType");
        string parentKeyName = GetString(values, "ParentKeyName");
        bool systemComponent = GetInt64(values, "SystemComponent") == 1;
        bool noRemove = GetInt64(values, "NoRemove") == 1;
        bool windowsInstaller = GetInt64(values, "WindowsInstaller") == 1;
        long? estimatedSizeKb = GetInt64(values, "EstimatedSize");
        long? estimatedSizeBytes = estimatedSizeKb is > 0 and <= long.MaxValue / 1024
            ? estimatedSizeKb * 1024
            : null;

        bool isSystemEntry = IsSystemLike(
            name,
            publisher,
            releaseType,
            parentKeyName,
            systemComponent);

        string identity = string.Join(
            "\u001f",
            Normalize(name),
            Normalize(version),
            Normalize(publisher),
            Normalize(uninstall));

        return new InstalledApplication
        {
            Id = CreateId(identity),
            DisplayName = name.Trim(),
            DisplayVersion = version.Trim(),
            Publisher = publisher.Trim(),
            InstallDate = ParseInstallDate(GetString(values, "InstallDate")),
            InstallLocation = ExpandPath(GetString(values, "InstallLocation")),
            DisplayIconPath = ExpandPath(GetString(values, "DisplayIcon")),
            ReportedSizeBytes = estimatedSizeBytes,
            UninstallString = uninstall.Trim(),
            QuietUninstallString = quietUninstall.Trim(),
            ModifyPath = GetString(values, "ModifyPath").Trim(),
            IsSystemComponent = systemComponent,
            IsSystemEntry = isSystemEntry,
            CanUninstall = !noRemove && !string.IsNullOrWhiteSpace(uninstall),
            RequiresElevation = requiresElevation && windowsInstaller,
            IsWindowsInstaller = windowsInstaller,
            NoRemove = noRemove,
            ReleaseType = releaseType.Trim(),
            ParentKeyName = parentKeyName.Trim(),
            Architecture = architecture,
            Source = ApplicationSource.Registry,
            RegistryPath = registryPath
        };
    }

    public static DateTime? ParseInstallDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string[] formats = ["yyyyMMdd", "yyyy-MM-dd", "yyyy/MM/dd", "MM/dd/yyyy"];
        return DateTime.TryParseExact(
            value.Trim(),
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out DateTime parsed)
            ? parsed
            : DateTime.TryParse(value, CultureInfo.CurrentCulture, out parsed) ? parsed : null;
    }

    private static string GetString(
        IReadOnlyDictionary<string, object?> values,
        string key) =>
        values.TryGetValue(key, out object? value)
            ? Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty
            : string.Empty;

    private static long? GetInt64(
        IReadOnlyDictionary<string, object?> values,
        string key)
    {
        if (!values.TryGetValue(key, out object? value) || value is null)
            return null;

        try
        {
            return Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (
            exception is FormatException ||
            exception is InvalidCastException ||
            exception is OverflowException)
        {
            return null;
        }
    }

    private static bool IsSystemLike(
        string name,
        string publisher,
        string releaseType,
        string parentKeyName,
        bool systemComponent)
    {
        if (systemComponent ||
            !string.IsNullOrWhiteSpace(releaseType) ||
            !string.IsNullOrWhiteSpace(parentKeyName))
        {
            return true;
        }

        string[] updateTerms =
        [
            "Security Update", "Update for", "Hotfix", "Service Pack",
            "安全更新", "更新程序", "修补程序"
        ];
        if (updateTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase)))
            return true;

        bool looksLikeKb = name.Contains("(KB", StringComparison.OrdinalIgnoreCase);
        bool looksLikeDriver =
            name.Contains(" Driver", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("驱动", StringComparison.OrdinalIgnoreCase);
        return looksLikeKb ||
               looksLikeDriver ||
               (publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) &&
                name.StartsWith("KB", StringComparison.OrdinalIgnoreCase));
    }

    private static string ExpandPath(string value) =>
        string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));

    private static string Normalize(string value) =>
        string.Join(' ', value.Trim().Split(
            (char[]?)null,
            StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    private static string CreateId(string identity)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash[..12]).ToLowerInvariant();
    }
}
