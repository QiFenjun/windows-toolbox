using System.IO;

namespace WindowsToolbox.Modules.LockInspector.Services;

internal sealed class ScanFileEnumerator
{
    public int Skipped { get; private set; }
    public bool WasLimited { get; private set; }
    private int _directories;
    private readonly EnumerationOptions _options = new() { RecurseSubdirectories = false, IgnoreInaccessible = false, AttributesToSkip = 0 };

    public IEnumerable<string> Enumerate(string root, bool recursive, CancellationToken token, int depth = 0)
    {
        token.ThrowIfCancellationRequested();
        // ponytail: bound depth/directory count as well as files; raise only after measured need.
        if (depth > 64 || ++_directories > LockScanService.FullScanLimit) { WasLimited = true; yield break; }
        if (!IsDirectoryAllowed(root)) yield break;
        foreach (string file in ReadEntries(root, false, token)) yield return file;
        if (!recursive) yield break;
        foreach (string directory in ReadEntries(root, true, token))
        {
            if (WasLimited) yield break;
            foreach (string file in Enumerate(directory, true, token, depth + 1)) yield return file;
        }
    }

    private bool IsDirectoryAllowed(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path) ||
                path.StartsWith(@"\\", StringComparison.Ordinal) ||
                (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            { Skipped++; return false; }
            return true;
        }
        catch (Exception ex) when (IsFileError(ex)) { Skipped++; return false; }
    }

    private IEnumerable<string> ReadEntries(string directory, bool directories, CancellationToken token)
    {
        IEnumerator<string>? enumerator = null;
        try
        {
            enumerator = (directories ? Directory.EnumerateDirectories(directory, "*", _options)
                : Directory.EnumerateFiles(directory, "*", _options)).GetEnumerator();
        }
        catch (Exception ex) when (IsFileError(ex)) { Skipped++; }
        if (enumerator is null) yield break;
        using (enumerator)
        {
            while (true)
            {
                token.ThrowIfCancellationRequested();
                bool next;
                try { next = enumerator.MoveNext(); }
                catch (Exception ex) when (IsFileError(ex)) { Skipped++; yield break; }
                if (!next) yield break;
                yield return enumerator.Current;
            }
        }
    }

    internal static bool IsFileError(Exception ex) => ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException;
}
