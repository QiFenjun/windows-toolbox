using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.FileTools.Models;

namespace WindowsToolbox.Modules.FileTools.Services;

public sealed class FileInfoService
{
    public FileInfoSnapshot Read(string path)
    {
        string fullPath = Path.GetFullPath(path);
        if (Directory.Exists(fullPath))
        {
            DirectoryInfo directory = new(fullPath);
            return new FileInfoSnapshot
            {
                Path = fullPath,
                Exists = true,
                IsDirectory = true,
                CreatedTime = directory.CreationTime,
                ModifiedTime = directory.LastWriteTime,
                LastAccessTime = directory.LastAccessTime,
                Attributes = directory.Attributes
            };
        }
        if (!File.Exists(fullPath))
            return new FileInfoSnapshot { Path = fullPath, Exists = false };

        FileInfo file = new(fullPath);
        FileVersionInfo version = FileVersionInfo.GetVersionInfo(fullPath);
        return new FileInfoSnapshot
        {
            Path = fullPath,
            Exists = true,
            IsDirectory = false,
            Size = file.Length,
            CreatedTime = file.CreationTime,
            ModifiedTime = file.LastWriteTime,
            LastAccessTime = file.LastAccessTime,
            Attributes = file.Attributes,
            FileVersion = version.FileVersion ?? string.Empty,
            ProductVersion = version.ProductVersion ?? string.Empty,
            Company = version.CompanyName ?? string.Empty,
            Description = version.FileDescription ?? string.Empty
        };
    }

    public async Task<FolderSizeResult> CalculateFolderSizeAsync(
        string path,
        IProgress<FolderSizeProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(path);
        if (!Directory.Exists(root))
            return new FolderSizeResult(0, 0, 0, 1, false);

        long files = 0, directories = 0, bytes = 0, skipped = 0;
        Stack<string> pending = new([root]);
        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directoryPath = pending.Pop();
            try
            {
                DirectoryInfo directory = new(directoryPath);
                if (directory.Attributes.HasFlag(FileAttributes.ReparsePoint))
                {
                    skipped++;
                    continue;
                }
                directories++;
                foreach (FileInfo file in directory.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { bytes += file.Length; files++; }
                    catch (IOException) { skipped++; }
                    catch (UnauthorizedAccessException) { skipped++; }
                }
                foreach (DirectoryInfo child in directory.EnumerateDirectories())
                {
                    try
                    {
                        if (child.Attributes.HasFlag(FileAttributes.ReparsePoint)) skipped++;
                        else pending.Push(child.FullName);
                    }
                    catch (IOException) { skipped++; }
                    catch (UnauthorizedAccessException) { skipped++; }
                }
            }
            catch (PathTooLongException) { skipped++; }
            catch (IOException) { skipped++; }
            catch (UnauthorizedAccessException) { skipped++; }

            progress?.Report(new FolderSizeProgress { FilesScanned = files, DirectoriesScanned = directories, TotalBytes = bytes, Skipped = skipped });
            await Task.Yield();
        }
        return new FolderSizeResult(files, directories, bytes, skipped, false);
    }
}
