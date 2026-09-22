using System.IO;
using System.Text.Json.Serialization;

namespace WindowsToolbox.Modules.QuickLaunch.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum QuickLaunchItemType
{
    Application,
    Folder,
    File,
    Url
}

public sealed class QuickLaunchItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string Target { get; set; } = string.Empty;
    public QuickLaunchItemType Type { get; set; }
    public string Group { get; set; } = string.Empty;
    public bool IsPinned { get; set; }
    public int Order { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset? LastUsedAt { get; set; }
    public int LaunchCount { get; set; }
    public string Arguments { get; set; } = string.Empty;
    public string WorkingDirectory { get; set; } = string.Empty;
}

public sealed class QuickLaunchGroup
{
    public string Name { get; set; } = string.Empty;
    public int Order { get; set; }
}

public sealed class QuickLaunchDocument
{
    public int DataVersion { get; set; } = 1;
    public List<QuickLaunchItem> Items { get; set; } = [];
    public List<QuickLaunchGroup> Groups { get; set; } = [];
}

public sealed record QuickLaunchLoadResult(
    IReadOnlyList<QuickLaunchItem> Items,
    IReadOnlyList<QuickLaunchGroup> Groups,
    bool WasCorrupt,
    string? CorruptBackupPath = null);

public sealed record QuickLaunchLaunchResult(bool Success, string Message);

public static class QuickLaunchItemRules
{
    public static bool IsUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri) &&
        (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
         uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidTarget(QuickLaunchItemType type, string? target)
    {
        if (string.IsNullOrWhiteSpace(target)) return false;
        return type switch
        {
            QuickLaunchItemType.Application => Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase),
            QuickLaunchItemType.Folder => !IsUrl(target),
            QuickLaunchItemType.File => !IsUrl(target),
            QuickLaunchItemType.Url => IsUrl(target),
            _ => false
        };
    }

    public static bool TargetExists(QuickLaunchItem item) => item.Type switch
    {
        QuickLaunchItemType.Application or QuickLaunchItemType.File => File.Exists(item.Target),
        QuickLaunchItemType.Folder => Directory.Exists(item.Target),
        QuickLaunchItemType.Url => IsUrl(item.Target),
        _ => false
    };

    public static string NormalizeKey(QuickLaunchItem item)
    {
        string target = item.Type == QuickLaunchItemType.Url
            ? item.Target.Trim()
            : NormalizePath(item.Target);
        return $"{item.Type}|{target}|{item.Arguments}|{item.WorkingDirectory}";
    }

    public static string NormalizePath(string path)
    {
        try { return Path.GetFullPath(path.Trim()); }
        catch (Exception) when (path is not null) { return path.Trim(); }
    }

    public static string TypeText(QuickLaunchItemType type) => type switch
    {
        QuickLaunchItemType.Application => "应用 / Application",
        QuickLaunchItemType.Folder => "文件夹 / Folder",
        QuickLaunchItemType.File => "文件 / File",
        QuickLaunchItemType.Url => "网页 / URL",
        _ => type.ToString()
    };

    public static string FallbackGlyph(QuickLaunchItemType type) => type switch
    {
        QuickLaunchItemType.Application => "\uE756",
        QuickLaunchItemType.Folder => "\uE8B7",
        QuickLaunchItemType.File => "\uE8A5",
        QuickLaunchItemType.Url => "\uE774",
        _ => "\uE8A5"
    };
}
