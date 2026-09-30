using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly SemaphoreSlim _saveLock = new(1, 1);
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Settings { get; private set; } = new();
    public SettingsService() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsToolbox",
        "settings.json")) { }

    public SettingsService(string settingsFilePath) => SettingsFilePath =
        string.IsNullOrWhiteSpace(settingsFilePath)
            ? throw new ArgumentException("设置文件路径不能为空。", nameof(settingsFilePath))
            : settingsFilePath;

    public string SettingsFilePath { get; }

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            await using FileStream stream = File.OpenRead(SettingsFilePath);
            Settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions)
                .ConfigureAwait(false) ?? new AppSettings();
            Settings.Language = string.Equals(Settings.Language, "en-US", StringComparison.OrdinalIgnoreCase)
                ? "en-US"
                : "zh-CN";
        }
        catch (JsonException)
        {
            Settings = new AppSettings();
        }
        catch (IOException)
        {
            Settings = new AppSettings();
        }
        catch (UnauthorizedAccessException)
        {
            Settings = new AppSettings();
        }
    }

    public async Task SaveAsync()
    {
        // Callers update AppSettings on the UI dispatcher. Copy mutable collections here, then serialize/write away from it.
        AppSettings snapshot = Settings.CreateSnapshot();
        await _saveLock.WaitAsync().ConfigureAwait(false);
        try
        {
            await Task.Run(async () =>
            {
                string? directory = Path.GetDirectoryName(SettingsFilePath);
                if (directory is not null) Directory.CreateDirectory(directory);
                string temporaryPath = SettingsFilePath + ".tmp";
                try
                {
                    await using (FileStream stream = new(temporaryPath, FileMode.Create, FileAccess.Write,
                        FileShare.None, 4096, FileOptions.Asynchronous))
                    {
                        await JsonSerializer.SerializeAsync(stream, snapshot, _jsonOptions).ConfigureAwait(false);
                        await stream.FlushAsync().ConfigureAwait(false);
                    }
                    File.Move(temporaryPath, SettingsFilePath, overwrite: true);
                }
                catch
                {
                    try { File.Delete(temporaryPath); } catch (IOException) { }
                    throw;
                }
            }).ConfigureAwait(false);
        }
        finally { _saveLock.Release(); }
    }
}
