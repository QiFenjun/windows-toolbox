using System.Security.Cryptography;
using System.IO;

namespace WindowsToolbox.Modules.FileTools.Models;

public enum FileToolTab
{
    Rename,
    Hash,
    Path,
    Info
}

public enum RenameCaseMode
{
    None,
    Lowercase,
    Uppercase,
    TitleCase
}

public sealed class RenameRuleOptions
{
    public string Prefix { get; init; } = string.Empty;
    public string Suffix { get; init; } = string.Empty;
    public string Find { get; init; } = string.Empty;
    public string Replace { get; init; } = string.Empty;
    public bool MatchCase { get; init; }
    public bool NumberingEnabled { get; init; }
    public int NumberStart { get; init; } = 1;
    public int NumberStep { get; init; } = 1;
    public int NumberDigits { get; init; } = 3;
    public string NumberPrefix { get; init; } = string.Empty;
    public string NumberSuffix { get; init; } = "_";
    public RenameCaseMode CaseMode { get; init; }
    public string NewExtension { get; init; } = string.Empty;
}

public enum RenameItemStatus
{
    Ready,
    Unchanged,
    Conflict,
    Invalid,
    Missing,
    Failed,
    Succeeded
}

public sealed class RenamePreviewItem
{
    public RenamePreviewItem(string originalPath, string newPath, RenameItemStatus status, string message = "")
    {
        OriginalPath = originalPath;
        NewPath = newPath;
        Status = status;
        Message = message;
    }

    public string OriginalPath { get; }
    public string NewPath { get; set; }
    public RenameItemStatus Status { get; set; }
    public string Message { get; set; }
    public string OriginalName => Path.GetFileName(OriginalPath);
    public string NewName => Path.GetFileName(NewPath);
    public string StatusText => Status switch
    {
        RenameItemStatus.Ready => "就绪",
        RenameItemStatus.Unchanged => "未改变",
        RenameItemStatus.Conflict => "冲突",
        RenameItemStatus.Invalid => "无效",
        RenameItemStatus.Missing => "文件不存在",
        RenameItemStatus.Failed => "失败",
        RenameItemStatus.Succeeded => "已完成",
        _ => Status.ToString()
    };
    public bool CanApply => Status == RenameItemStatus.Ready;
}

public sealed record RenameBatchResult(
    int Succeeded,
    int Failed,
    IReadOnlyList<RenamePreviewItem> Items,
    string Message);

public sealed class HashProgress
{
    public string CurrentFile { get; init; } = string.Empty;
    public long CurrentBytes { get; init; }
    public long CurrentLength { get; init; }
    public long TotalBytes { get; init; }
    public long ProcessedTotalBytes { get; init; }
    public int CurrentIndex { get; init; }
    public int TotalFiles { get; init; }
    public double CurrentFraction => CurrentLength <= 0 ? 0 : Math.Clamp((double)CurrentBytes / CurrentLength, 0, 1);
    public double OverallFraction => TotalBytes <= 0 ? 0 : Math.Clamp((double)ProcessedTotalBytes / TotalBytes, 0, 1);
}

public enum HashItemStatus
{
    Ready,
    Completed,
    Cancelled,
    Missing,
    FileChangedDuringHash,
    Failed
}

public sealed class HashResult
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName => Path.GetFileName(FilePath);
    public long Size { get; init; }
    public string Algorithm { get; init; } = "SHA-256";
    public string Hash { get; init; } = string.Empty;
    public HashItemStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string StatusText => Status switch
    {
        HashItemStatus.Completed => "完成",
        HashItemStatus.Cancelled => "已取消",
        HashItemStatus.Missing => "不存在",
        HashItemStatus.FileChangedDuringHash => "文件已变化",
        HashItemStatus.Failed => "失败",
        _ => "等待"
    };
}

public enum PathOutputKind
{
    FullPath,
    FileName,
    FileStem,
    Extension,
    ParentDirectory,
    ForwardSlash,
    QuotedWindowsPath,
    PowerShellLiteralPath,
    FileUri
}

public sealed record PathResult(string InputPath, PathOutputKind Kind, string Value)
{
    public string KindText => Kind switch
    {
        PathOutputKind.FullPath => "完整路径",
        PathOutputKind.FileName => "文件名",
        PathOutputKind.FileStem => "文件名（无扩展名）",
        PathOutputKind.Extension => "扩展名",
        PathOutputKind.ParentDirectory => "父目录",
        PathOutputKind.ForwardSlash => "正斜杠路径",
        PathOutputKind.QuotedWindowsPath => "带引号路径",
        PathOutputKind.PowerShellLiteralPath => "PowerShell LiteralPath",
        PathOutputKind.FileUri => "File URI",
        _ => Kind.ToString()
    };
}

public sealed class FileInfoSnapshot
{
    public string Path { get; init; } = string.Empty;
    public string Name => System.IO.Path.GetFileName(System.IO.Path.TrimEndingDirectorySeparator(Path));
    public bool Exists { get; init; }
    public bool IsDirectory { get; init; }
    public long Size { get; init; }
    public DateTime CreatedTime { get; init; }
    public DateTime ModifiedTime { get; init; }
    public DateTime LastAccessTime { get; init; }
    public FileAttributes Attributes { get; init; }
    public bool IsReadOnly => Attributes.HasFlag(FileAttributes.ReadOnly);
    public bool IsHidden => Attributes.HasFlag(FileAttributes.Hidden);
    public bool IsSystem => Attributes.HasFlag(FileAttributes.System);
    public bool IsArchive => Attributes.HasFlag(FileAttributes.Archive);
    public string Extension => IsDirectory ? string.Empty : System.IO.Path.GetExtension(Path);
    public string FileVersion { get; init; } = string.Empty;
    public string ProductVersion { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string StatusText => Exists ? (IsDirectory ? "文件夹" : "文件") : "不存在";
}

public sealed class FolderSizeProgress
{
    public long FilesScanned { get; init; }
    public long DirectoriesScanned { get; init; }
    public long TotalBytes { get; init; }
    public long Skipped { get; init; }
}

public sealed record FolderSizeResult(long FilesScanned, long DirectoriesScanned, long TotalBytes, long Skipped, bool Cancelled)
{
    public string Summary => $"文件 {FilesScanned:N0} 个，目录 {DirectoriesScanned:N0} 个，大小 {FormatBytes(TotalBytes)}，跳过 {Skipped:N0} 项";

    public static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        string[] units = ["KB", "MB", "GB", "TB"];
        double value = bytes;
        int index = -1;
        do { value /= 1024; index++; } while (value >= 1024 && index < units.Length - 1);
        return $"{value:0.##} {units[index]}";
    }
}

public static class HashAlgorithmCatalog
{
    public static IReadOnlyList<string> Names { get; } = ["SHA-256", "SHA-512", "MD5"];

    public static HashAlgorithmName Resolve(string name) => name switch
    {
        "SHA-512" => HashAlgorithmName.SHA512,
        "MD5" => HashAlgorithmName.MD5,
        _ => HashAlgorithmName.SHA256
    };
}
