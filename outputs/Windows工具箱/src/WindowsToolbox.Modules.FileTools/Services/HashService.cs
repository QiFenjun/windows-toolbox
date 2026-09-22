using System.Security.Cryptography;
using System.Text;
using System.IO;
using WindowsToolbox.Modules.FileTools.Models;

namespace WindowsToolbox.Modules.FileTools.Services;

public sealed class HashService
{
    private const int BufferSize = 1024 * 1024;

    public async Task<IReadOnlyList<HashResult>> ComputeAsync(
        IEnumerable<string> paths,
        string algorithm,
        IProgress<HashProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string[] files = paths.Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        long totalBytes = files.Where(File.Exists).Sum(path => new FileInfo(path).Length);
        long processed = 0;
        List<HashResult> results = [];
        for (int index = 0; index < files.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            HashResult result = await ComputeFileAsync(files[index], algorithm, totalBytes, processed, index, files.Length, progress, cancellationToken).ConfigureAwait(false);
            results.Add(result);
            processed += result.Size;
            if (result.Status == HashItemStatus.Cancelled)
                break;
        }
        return results;
    }

    public async Task<HashResult> ComputeFileAsync(
        string path,
        string algorithm,
        long totalBytes = 0,
        long processedTotalBytes = 0,
        int currentIndex = 0,
        int totalFiles = 1,
        IProgress<HashProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        string fullPath = Path.GetFullPath(path);
        if (!File.Exists(fullPath))
            return new HashResult { FilePath = fullPath, Algorithm = algorithm, Status = HashItemStatus.Missing, Message = "文件不存在。" };

        FileInfo before = new(fullPath);
        long size = before.Length;
        if (totalBytes <= 0) totalBytes = size;
        try
        {
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmCatalog.Resolve(algorithm));
            await using FileStream stream = new(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);
            byte[] buffer = new byte[BufferSize];
            long readTotal = 0;
            int read;
            while ((read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
            {
                hash.AppendData(buffer, 0, read);
                readTotal += read;
                progress?.Report(new HashProgress
                {
                    CurrentFile = fullPath,
                    CurrentBytes = readTotal,
                    CurrentLength = size,
                    TotalBytes = totalBytes,
                    ProcessedTotalBytes = processedTotalBytes + readTotal,
                    CurrentIndex = currentIndex + 1,
                    TotalFiles = totalFiles
                });
            }
            FileInfo after = new(fullPath);
            if (after.Length != before.Length || after.LastWriteTimeUtc != before.LastWriteTimeUtc)
                return new HashResult { FilePath = fullPath, Size = size, Algorithm = algorithm, Status = HashItemStatus.FileChangedDuringHash, Message = "文件在计算期间发生变化，请重新计算。" };

            return new HashResult
            {
                FilePath = fullPath,
                Size = size,
                Algorithm = algorithm,
                Hash = Convert.ToHexString(hash.GetHashAndReset()),
                Status = HashItemStatus.Completed,
                Message = "校验完成。"
            };
        }
        catch (OperationCanceledException)
        {
            return new HashResult { FilePath = fullPath, Size = size, Algorithm = algorithm, Status = HashItemStatus.Cancelled, Message = "已取消，不保留不完整 Hash。" };
        }
        catch (IOException exception)
        {
            return new HashResult { FilePath = fullPath, Size = size, Algorithm = algorithm, Status = HashItemStatus.Failed, Message = $"文件正在使用或无法访问：{exception.Message}" };
        }
        catch (UnauthorizedAccessException)
        {
            return new HashResult { FilePath = fullPath, Size = size, Algorithm = algorithm, Status = HashItemStatus.Failed, Message = "当前权限不足，无法读取文件。" };
        }
    }

    public static bool Verify(string expected, string actual) =>
        string.Equals(expected.Trim(), actual.Trim(), StringComparison.OrdinalIgnoreCase);

    public async Task GenerateSha256SumsAsync(
        IEnumerable<HashResult> results,
        string targetPath,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        string fullTarget = Path.GetFullPath(targetPath);
        if (File.Exists(fullTarget) && !overwrite)
            throw new IOException("目标文件已经存在，需要明确确认覆盖。");

        IEnumerable<HashResult> completed = results.Where(result => result.Status == HashItemStatus.Completed && !string.IsNullOrEmpty(result.Hash));
        StringBuilder builder = new();
        foreach (HashResult result in completed)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string fileName = Path.GetFileName(result.FilePath).Replace("\r", string.Empty).Replace("\n", string.Empty);
            builder.Append(result.Hash).Append("  ").Append(fileName).AppendLine();
        }
        await File.WriteAllTextAsync(fullTarget, builder.ToString(), cancellationToken).ConfigureAwait(false);
    }
}
