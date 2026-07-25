using System.IO;
using System.Text.Json;
using WindowsToolbox.Modules.InstalledApps.Models;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class ApplicationSizeCache
{
    private readonly string _cachePath;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private Dictionary<string, CachedApplicationSize>? _entries;

    public ApplicationSizeCache(string? cachePath = null)
    {
        _cachePath = cachePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WindowsToolbox",
            "Cache",
            "installed-app-sizes.json");
    }

    public async Task<CachedApplicationSize?> GetAsync(
        InstalledApplication application,
        CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        if (_entries is null ||
            !_entries.TryGetValue(application.Id, out CachedApplicationSize? entry) ||
            !IsValid(entry, application))
        {
            return null;
        }

        return entry;
    }

    public async Task SetAsync(
        InstalledApplication application,
        ApplicationSizeInfo size,
        CancellationToken cancellationToken)
    {
        await EnsureLoadedAsync(cancellationToken);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _entries ??= new Dictionary<string, CachedApplicationSize>(StringComparer.OrdinalIgnoreCase);
            _entries[application.Id] = new CachedApplicationSize(
                application.Id,
                application.InstallLocation,
                application.DisplayVersion,
                size.SizeBytes,
                size.ScannedAt);

            string? directory = Path.GetDirectoryName(_cachePath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            string temporaryPath = _cachePath + ".tmp";
            await using (FileStream stream = new(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                4096,
                useAsync: true))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    _entries,
                    cancellationToken: cancellationToken);
            }

            File.Move(temporaryPath, _cachePath, overwrite: true);
        }
        finally
        {
            _gate.Release();
        }
    }

    public static bool IsValid(
        CachedApplicationSize entry,
        InstalledApplication application) =>
        string.Equals(entry.ApplicationId, application.Id, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            NormalizePath(entry.InstallLocation),
            NormalizePath(application.InstallLocation),
            StringComparison.OrdinalIgnoreCase) &&
        string.Equals(
            entry.ApplicationVersion ?? string.Empty,
            application.DisplayVersion ?? string.Empty,
            StringComparison.OrdinalIgnoreCase) &&
        entry.SizeBytes >= 0;

    private async Task EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_entries is not null)
            return;

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_entries is not null)
                return;

            _entries = new Dictionary<string, CachedApplicationSize>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(_cachePath))
                return;

            try
            {
                await using FileStream stream = new(
                    _cachePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    useAsync: true);
                Dictionary<string, CachedApplicationSize>? loaded =
                    await JsonSerializer.DeserializeAsync<Dictionary<string, CachedApplicationSize>>(
                        stream,
                        cancellationToken: cancellationToken);
                if (loaded is not null)
                    _entries = new Dictionary<string, CachedApplicationSize>(loaded, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception exception) when (
                exception is JsonException ||
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                // 损坏的缓存会被忽略，下一次扫描将重建。
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception exception) when (
            exception is ArgumentException ||
            exception is NotSupportedException ||
            exception is PathTooLongException)
        {
            return path.Trim();
        }
    }
}

public sealed record CachedApplicationSize(
    string ApplicationId,
    string InstallLocation,
    string ApplicationVersion,
    long SizeBytes,
    DateTime ScannedAt);
