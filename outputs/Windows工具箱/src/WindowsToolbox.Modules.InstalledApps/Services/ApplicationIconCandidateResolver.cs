using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>按可靠性生成图标来源候选，不递归扫描安装目录，也不采用卸载器。</summary>
public static class ApplicationIconCandidateResolver
{
    private static readonly string[] UnsafeExecutableTerms =
    [
        "uninstall", "unins", "updater", "update", "setup", "installer",
        "crash", "helper", "service", "reporter", "msiexec"
    ];

    public static IReadOnlyList<ApplicationIconCandidate> Resolve(InstalledApplication application)
    {
        List<ApplicationIconCandidate> candidates = [];
        ParsedDisplayIcon? displayIcon = DisplayIconParser.Parse(application.DisplayIconPath);
        if (displayIcon is not null && IsShellIconFile(displayIcon.Path))
        {
            candidates.Add(new ApplicationIconCandidate(
                displayIcon.Path,
                displayIcon.IconIndex,
                ApplicationIconSource.DisplayIcon));
        }

        string? primaryExecutable = FindReliableExecutable(application);
        if (!string.IsNullOrWhiteSpace(primaryExecutable) &&
            !candidates.Any(candidate => string.Equals(
                candidate.Path,
                primaryExecutable,
                StringComparison.OrdinalIgnoreCase) && candidate.IconIndex == 0))
        {
            candidates.Add(new ApplicationIconCandidate(
                primaryExecutable,
                0,
                ApplicationIconSource.InstallLocation));
        }

        return candidates;
    }

    private static string? FindReliableExecutable(InstalledApplication application)
    {
        string location = application.InstallLocation;
        if (string.IsNullOrWhiteSpace(location))
            return null;

        try
        {
            if (File.Exists(location) && IsSafeExecutable(location))
                return location;

            if (!Directory.Exists(location))
                return null;

            string normalizedName = Normalize(application.DisplayName);
            string normalizedPublisher = Normalize(application.Publisher);
            (string Path, int Score)? best = null;
            foreach (string executable in Directory.EnumerateFiles(location, "*.exe", SearchOption.TopDirectoryOnly))
            {
                if (!IsSafeExecutable(executable))
                    continue;

                int score = ScoreExecutable(executable, normalizedName, normalizedPublisher);
                if (best is null || score > best.Value.Score ||
                    (score == best.Value.Score && string.Compare(executable, best.Value.Path, StringComparison.OrdinalIgnoreCase) < 0))
                {
                    best = (executable, score);
                }
            }

            return best is { Score: >= 70 } ? best.Value.Path : null;
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is System.Security.SecurityException ||
            exception is ArgumentException)
        {
            return null;
        }
    }

    private static int ScoreExecutable(string path, string normalizedName, string normalizedPublisher)
    {
        string fileName = Normalize(Path.GetFileNameWithoutExtension(path));
        int score = fileName == normalizedName ? 120 :
            fileName.Contains(normalizedName, StringComparison.OrdinalIgnoreCase) ||
            normalizedName.Contains(fileName, StringComparison.OrdinalIgnoreCase) ? 80 : 0;

        try
        {
            FileVersionInfo version = FileVersionInfo.GetVersionInfo(path);
            score += ScoreMetadata(version.ProductName, normalizedName, 70);
            score += ScoreMetadata(version.FileDescription, normalizedName, 45);
            score += ScoreMetadata(version.CompanyName, normalizedPublisher, 15);
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is System.ComponentModel.Win32Exception)
        {
            // 文件正在更新或元数据不可读取时，仍可使用文件名分数。
        }

        return score;
    }

    private static int ScoreMetadata(string? value, string expected, int score) =>
        string.IsNullOrWhiteSpace(value) || string.IsNullOrWhiteSpace(expected)
            ? 0
            : Normalize(value).Contains(expected, StringComparison.OrdinalIgnoreCase) ? score : 0;

    private static bool IsShellIconFile(string path)
    {
        try { return File.Exists(path); }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is ArgumentException) { return false; }
    }

    private static bool IsSafeExecutable(string path)
    {
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            return false;

        string name = Path.GetFileNameWithoutExtension(path);
        return !UnsafeExecutableTerms.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string Normalize(string value) =>
        new string(value.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
}
