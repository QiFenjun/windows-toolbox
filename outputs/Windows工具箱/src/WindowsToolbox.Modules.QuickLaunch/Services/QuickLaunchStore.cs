using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsToolbox.Modules.QuickLaunch.Models;

namespace WindowsToolbox.Modules.QuickLaunch.Services;

public sealed class QuickLaunchStore : IQuickLaunchStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public QuickLaunchStore(string? root = null)
    {
        string basePath = root ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        FilePath = Path.Combine(basePath, "WindowsToolbox", "QuickLaunch", "items.json");
    }

    public string FilePath { get; }
    public string DirectoryPath => Path.GetDirectoryName(FilePath)!;

    public async Task<QuickLaunchLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(FilePath)) return new([], [], false);
        try
        {
            await using FileStream stream = new(FilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
            QuickLaunchDocument document = await JsonSerializer.DeserializeAsync<QuickLaunchDocument>(stream, _options, cancellationToken).ConfigureAwait(false)
                ?? new QuickLaunchDocument();
            return new(document.Items ?? [], document.Groups ?? [], false);
        }
        catch (JsonException)
        {
            string? backup = BackupCorruptFile();
            return new([], [], true, backup);
        }
        catch (NotSupportedException)
        {
            string? backup = BackupCorruptFile();
            return new([], [], true, backup);
        }
        catch (IOException)
        {
            return new([], [], false);
        }
        catch (UnauthorizedAccessException)
        {
            return new([], [], false);
        }
    }

    public async Task SaveAsync(IEnumerable<QuickLaunchItem> items, IEnumerable<QuickLaunchGroup> groups, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(DirectoryPath);
        string tempPath = Path.Combine(DirectoryPath, $"items.{Guid.NewGuid():N}.tmp");
        QuickLaunchDocument document = new()
        {
            DataVersion = 1,
            Items = items.ToList(),
            Groups = groups.ToList()
        };
        try
        {
            await using (FileStream stream = new(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await JsonSerializer.SerializeAsync(stream, document, _options, cancellationToken).ConfigureAwait(false);

            if (File.Exists(FilePath))
                File.Replace(tempPath, FilePath, null, true);
            else
                File.Move(tempPath, FilePath);
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
        }
    }

    private string? BackupCorruptFile()
    {
        try
        {
            string backup = Path.Combine(DirectoryPath, $"items.corrupt.{DateTimeOffset.Now:yyyyMMddHHmmssfff}.json");
            File.Move(FilePath, backup);
            return backup;
        }
        catch { return null; }
    }
}
