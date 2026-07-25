using System.IO;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class ApplicationSizeService(
    ApplicationSizeCache cache,
    int maxConcurrentScans = 2)
{
    private readonly SemaphoreSlim _scanGate = new(Math.Max(1, maxConcurrentScans));

    public async Task ApplyCachedSizeAsync(
        InstalledApplication application,
        CancellationToken cancellationToken)
    {
        CachedApplicationSize? cached = await cache.GetAsync(application, cancellationToken);
        if (cached is null)
            return;

        application.ScannedSizeBytes = cached.SizeBytes;
        application.ScannedAt = cached.ScannedAt;
    }

    public async Task<ApplicationSizeInfo> ScanAsync(
        InstalledApplication application,
        IProgress<SizeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(application);
        string path = application.InstallLocation;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            throw new DirectoryNotFoundException("软件安装位置不存在或无法访问。");

        await _scanGate.WaitAsync(cancellationToken);
        try
        {
            ApplicationSizeInfo result = await Task.Run(
                () => ScanDirectory(path, progress, cancellationToken),
                cancellationToken);
            try
            {
                await cache.SetAsync(application, result, cancellationToken);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                // 缓存写入失败不应丢弃已经完成的扫描结果。
            }
            return result;
        }
        finally
        {
            _scanGate.Release();
        }
    }

    public static bool ShouldSkip(FileAttributes attributes) =>
        (attributes & FileAttributes.ReparsePoint) != 0;

    private static ApplicationSizeInfo ScanDirectory(
        string rootPath,
        IProgress<SizeScanProgress>? progress,
        CancellationToken cancellationToken)
    {
        DirectoryInfo root = new(rootPath);
        if (ShouldSkip(root.Attributes))
            throw new IOException("安装目录是重解析点，已拒绝扫描。");

        Stack<DirectoryInfo> pending = new();
        HashSet<string> visited = new(StringComparer.OrdinalIgnoreCase);
        pending.Push(root);

        long totalBytes = 0;
        long fileCount = 0;
        int skipped = 0;

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            DirectoryInfo current = pending.Pop();
            string fullPath;
            try
            {
                fullPath = current.FullName.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar);
                if (!visited.Add(fullPath) || ShouldSkip(current.Attributes))
                {
                    skipped++;
                    continue;
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException)
            {
                skipped++;
                continue;
            }

            try
            {
                foreach (FileInfo file in current.EnumerateFiles())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (ShouldSkip(file.Attributes))
                        {
                            skipped++;
                            continue;
                        }

                        totalBytes = checked(totalBytes + file.Length);
                        fileCount++;
                        if (fileCount % 200 == 0)
                            progress?.Report(new SizeScanProgress(totalBytes, fileCount));
                    }
                    catch (Exception exception) when (
                        exception is IOException ||
                        exception is UnauthorizedAccessException ||
                        exception is System.Security.SecurityException ||
                        exception is OverflowException)
                    {
                        skipped++;
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException)
            {
                skipped++;
            }

            try
            {
                foreach (DirectoryInfo directory in current.EnumerateDirectories())
                {
                    try
                    {
                        if (ShouldSkip(directory.Attributes))
                        {
                            skipped++;
                            continue;
                        }

                        pending.Push(directory);
                    }
                    catch (Exception exception) when (
                        exception is IOException ||
                        exception is UnauthorizedAccessException ||
                        exception is System.Security.SecurityException)
                    {
                        skipped++;
                    }
                }
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is System.Security.SecurityException)
            {
                skipped++;
            }
        }

        progress?.Report(new SizeScanProgress(totalBytes, fileCount));
        return new ApplicationSizeInfo(totalBytes, DateTime.Now, fileCount, skipped);
    }
}
