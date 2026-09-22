using WindowsToolbox.Modules.FileTools.Models;
using System.IO;

namespace WindowsToolbox.Modules.FileTools.Services;

public sealed class PathToolsService
{
    public IReadOnlyList<PathResult> Convert(IEnumerable<string> paths, IEnumerable<PathOutputKind> kinds)
    {
        string[] normalized = paths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => Path.GetFullPath(path.Trim()))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        PathOutputKind[] outputKinds = kinds.Distinct().ToArray();
        return normalized.SelectMany(path => outputKinds.Select(kind => new PathResult(path, kind, Format(path, kind)))).ToArray();
    }

    public string Format(string input, PathOutputKind kind)
    {
        string fullPath = Path.GetFullPath(input.Trim());
        return kind switch
        {
            PathOutputKind.FullPath => fullPath,
            PathOutputKind.FileName => Path.GetFileName(fullPath),
            PathOutputKind.FileStem => Path.GetFileNameWithoutExtension(fullPath),
            PathOutputKind.Extension => Path.GetExtension(fullPath),
            PathOutputKind.ParentDirectory => Path.GetDirectoryName(fullPath) ?? string.Empty,
            PathOutputKind.ForwardSlash => fullPath.Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/'),
            PathOutputKind.QuotedWindowsPath => $"\"{fullPath.Replace("\"", "\\\"")}\"",
            PathOutputKind.PowerShellLiteralPath => $"'{fullPath.Replace("'", "''")}'",
            PathOutputKind.FileUri => new Uri(fullPath, UriKind.Absolute).AbsoluteUri,
            _ => fullPath
        };
    }
}
