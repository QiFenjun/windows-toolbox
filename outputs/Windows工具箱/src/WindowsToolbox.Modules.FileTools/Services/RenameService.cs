using System.Globalization;
using System.IO;
using System.Text;
using WindowsToolbox.Modules.FileTools.Models;

namespace WindowsToolbox.Modules.FileTools.Services;

public sealed class RenameService
{
    private static readonly char[] InvalidCharacters = Path.GetInvalidFileNameChars();
    private IReadOnlyList<RenamePreviewItem> _lastBatch = [];

    public IReadOnlyList<RenamePreviewItem> BuildPreview(IEnumerable<string> sourcePaths, RenameRuleOptions options)
    {
        ArgumentNullException.ThrowIfNull(sourcePaths);
        ArgumentNullException.ThrowIfNull(options);

        string[] paths = sourcePaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(NormalizePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        List<RenamePreviewItem> items = [];
        for (int index = 0; index < paths.Length; index++)
        {
            string source = paths[index];
            if (!File.Exists(source))
            {
                items.Add(new RenamePreviewItem(source, source, RenameItemStatus.Missing, "源文件不存在。"));
                continue;
            }

            string target = BuildTargetPath(source, index, options);
            RenameItemStatus status = RenameItemStatus.Ready;
            string message = string.Empty;
            string targetName = Path.GetFileName(target);
            if (string.IsNullOrWhiteSpace(targetName))
                (status, message) = (RenameItemStatus.Invalid, "目标名称不能为空。");
            else if (HasInvalidName(targetName))
                (status, message) = (RenameItemStatus.Invalid, "名称包含 Windows 不允许的字符或格式。");
            else if (string.Equals(source, target, StringComparison.Ordinal))
                (status, message) = (RenameItemStatus.Unchanged, "名称未改变。");
            else if (target.Length > 32767)
                (status, message) = (RenameItemStatus.Invalid, "目标路径过长。");

            items.Add(new RenamePreviewItem(source, target, status, message));
        }

        HashSet<string> sourceSet = items.Select(item => item.OriginalPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, List<RenamePreviewItem>> targetGroups = items
            .Where(item => item.Status is RenameItemStatus.Ready or RenameItemStatus.Unchanged)
            .GroupBy(item => item.NewPath, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (RenamePreviewItem item in items.Where(item => item.Status == RenameItemStatus.Ready))
        {
            if (targetGroups.TryGetValue(item.NewPath, out List<RenamePreviewItem>? group) && group.Count > 1)
            {
                item.Status = RenameItemStatus.Conflict;
                item.Message = "多个源文件生成了相同目标名称。";
            }
            else if (File.Exists(item.NewPath) && !sourceSet.Contains(item.NewPath))
            {
                item.Status = RenameItemStatus.Conflict;
                item.Message = "目标文件已经存在。";
            }
        }

        return items;
    }

    public async Task<RenameBatchResult> ApplyAsync(
        IReadOnlyList<RenamePreviewItem> preview,
        CancellationToken cancellationToken = default)
    {
        await Task.Yield();
        ArgumentNullException.ThrowIfNull(preview);
        List<RenamePreviewItem> items = preview.ToList();
        List<RenamePreviewItem> ready = items.Where(item => item.Status == RenameItemStatus.Ready).ToList();
        if (ready.Count == 0)
            return new RenameBatchResult(0, 0, items, "没有可执行的重命名项目。");

        foreach (RenamePreviewItem item in ready)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(item.OriginalPath))
            {
                item.Status = RenameItemStatus.Missing;
                item.Message = "执行前源文件已不存在。";
            }
        }
        ready = items.Where(item => item.Status == RenameItemStatus.Ready).ToList();
        HashSet<string> sourceSet = ready.Select(item => item.OriginalPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (RenamePreviewItem item in ready)
        {
            if (File.Exists(item.NewPath) && !sourceSet.Contains(item.NewPath))
            {
                item.Status = RenameItemStatus.Conflict;
                item.Message = "执行前目标文件已经存在。";
            }
        }
        ready = items.Where(item => item.Status == RenameItemStatus.Ready).ToList();
        if (ready.Count == 0)
            return new RenameBatchResult(0, items.Count(item => item.Status is RenameItemStatus.Failed or RenameItemStatus.Conflict or RenameItemStatus.Missing), items, "文件在执行前发生了变化。");

        List<(RenamePreviewItem Item, string Temp)> staged = [];
        try
        {
            foreach (RenamePreviewItem item in ready)
            {
                cancellationToken.ThrowIfCancellationRequested();
                string temporary = CreateTemporaryPath(item.OriginalPath);
                File.Move(item.OriginalPath, temporary);
                staged.Add((item, temporary));
            }

            foreach ((RenamePreviewItem item, string temporary) in staged)
            {
                cancellationToken.ThrowIfCancellationRequested();
                File.Move(temporary, item.NewPath);
                item.Status = RenameItemStatus.Succeeded;
                item.Message = "重命名成功。";
            }

            _lastBatch = items.Where(item => item.Status == RenameItemStatus.Succeeded).ToArray();
            return new RenameBatchResult(_lastBatch.Count, items.Count(item => item.Status != RenameItemStatus.Succeeded), items, "批量重命名完成。");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or OperationCanceledException)
        {
            foreach ((RenamePreviewItem item, string temporary) in staged.AsEnumerable().Reverse())
            {
                try
                {
                    if (File.Exists(item.NewPath))
                        File.Move(item.NewPath, temporary);
                    if (File.Exists(temporary))
                        File.Move(temporary, item.OriginalPath);
                    item.Status = RenameItemStatus.Failed;
                    item.Message = "操作失败，已尝试回滚。";
                }
                catch (Exception rollbackException) when (rollbackException is IOException or UnauthorizedAccessException)
                {
                    item.Status = RenameItemStatus.Failed;
                    item.Message = $"操作失败且无法自动恢复：{rollbackException.Message}";
                }
            }
            string message = exception is OperationCanceledException ? "操作已取消，已尝试回滚。" : "批量重命名失败，已尝试回滚。";
            return new RenameBatchResult(items.Count(item => item.Status == RenameItemStatus.Succeeded), items.Count(item => item.Status == RenameItemStatus.Failed), items, message);
        }
    }

    public async Task<RenameBatchResult> UndoLastBatchAsync(CancellationToken cancellationToken = default)
    {
        if (_lastBatch.Count == 0)
            return new RenameBatchResult(0, 0, [], "本次运行没有可撤销的重命名。");

        List<RenamePreviewItem> undoItems = _lastBatch
            .Select(item => new RenamePreviewItem(item.NewPath, item.OriginalPath, RenameItemStatus.Ready))
            .ToList();
        RenameBatchResult result = await ApplyAsync(undoItems, cancellationToken).ConfigureAwait(false);
        if (result.Succeeded == undoItems.Count)
            _lastBatch = [];
        return result with { Message = result.Succeeded == undoItems.Count ? "已撤销上次重命名（仅本次运行有效）。" : "无法安全撤销，部分文件需要人工处理。" };
    }

    private static string BuildTargetPath(string source, int index, RenameRuleOptions options)
    {
        string directory = Path.GetDirectoryName(source) ?? string.Empty;
        string stem = Path.GetFileNameWithoutExtension(source);
        string extension = Path.GetExtension(source);
        if (!string.IsNullOrWhiteSpace(options.Find))
        {
            StringComparison comparison = options.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            stem = ReplaceOrdinal(stem, options.Find, options.Replace, comparison);
        }

        stem = options.CaseMode switch
        {
            RenameCaseMode.Lowercase => stem.ToLowerInvariant(),
            RenameCaseMode.Uppercase => stem.ToUpperInvariant(),
            RenameCaseMode.TitleCase => CultureInfo.CurrentCulture.TextInfo.ToTitleCase(stem.ToLower(CultureInfo.CurrentCulture)),
            _ => stem
        };
        if (options.NumberingEnabled)
        {
            int number = options.NumberStart + index * options.NumberStep;
            string formatted = Math.Max(0, number).ToString($"D{Math.Clamp(options.NumberDigits, 1, 12)}", CultureInfo.InvariantCulture);
            stem = $"{options.NumberPrefix}{formatted}{options.NumberSuffix}{stem}";
        }
        stem = options.Prefix + stem + options.Suffix;
        if (!string.IsNullOrWhiteSpace(options.NewExtension))
            extension = "." + options.NewExtension.Trim().TrimStart('.');
        return Path.Combine(directory, stem + extension);
    }

    private static string ReplaceOrdinal(string value, string find, string replace, StringComparison comparison)
    {
        StringBuilder builder = new(value.Length);
        int cursor = 0;
        while (cursor < value.Length)
        {
            int index = value.IndexOf(find, cursor, comparison);
            if (index < 0) { builder.Append(value, cursor, value.Length - cursor); break; }
            builder.Append(value, cursor, index - cursor).Append(replace);
            cursor = index + find.Length;
        }
        return builder.ToString();
    }

    private static bool HasInvalidName(string name)
    {
        if (name.EndsWith(' ') || name.EndsWith('.')) return true;
        if (name.IndexOfAny(InvalidCharacters) >= 0 || name.Any(char.IsControl)) return true;
        string stem = Path.GetFileNameWithoutExtension(name).TrimEnd(' ', '.');
        string device = stem.Split('.')[0];
        return device.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
               device.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
               device.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
               device.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
               (device.Length == 4 && (device.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || device.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && device[3] is >= '1' and <= '9');
    }

    private static string NormalizePath(string path) => Path.GetFullPath(path);

    private static string CreateTemporaryPath(string source)
    {
        string directory = Path.GetDirectoryName(source) ?? throw new IOException("无法确定文件目录。");
        string temporary;
        do { temporary = Path.Combine(directory, $".windows-toolbox-rename-{Guid.NewGuid():N}.tmp"); }
        while (File.Exists(temporary));
        return temporary;
    }
}
