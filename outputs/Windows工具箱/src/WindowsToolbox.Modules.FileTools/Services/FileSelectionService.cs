using System.IO;

namespace WindowsToolbox.Modules.FileTools.Services;

/// <summary>用 Windows 路径语义去重用户选择的文件。</summary>
public sealed class FileSelectionService
{
    private readonly HashSet<string> _paths = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Add(IEnumerable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        foreach (string? path in paths)
        {
            if (string.IsNullOrWhiteSpace(path))
                continue;

            try { _paths.Add(Path.GetFullPath(path)); }
            catch (ArgumentException) { /* invalid input is ignored by the selection boundary */ }
        }
        return Items;
    }

    public IReadOnlyList<string> Items => _paths.OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool Remove(string path)
    {
        try { return _paths.Remove(Path.GetFullPath(path)); }
        catch (ArgumentException) { return false; }
    }

    public void Clear() => _paths.Clear();
}
