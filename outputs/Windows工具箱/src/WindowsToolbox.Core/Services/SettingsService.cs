using System.Text.Json;
using System.Text.Json.Serialization;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Core.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Settings { get; private set; } = new();
    public string SettingsFilePath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "WindowsToolbox",
        "settings.json");

    public async Task LoadAsync()
    {
        try
        {
            if (!File.Exists(SettingsFilePath))
                return;

            await using FileStream stream = File.OpenRead(SettingsFilePath);
            Settings = await JsonSerializer.DeserializeAsync<AppSettings>(stream, _jsonOptions)
                .ConfigureAwait(false) ?? new AppSettings();
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
        string? directory = Path.GetDirectoryName(SettingsFilePath);
        if (directory is not null)
            Directory.CreateDirectory(directory);

        await using FileStream stream = File.Create(SettingsFilePath);
        await JsonSerializer.SerializeAsync(stream, Settings, _jsonOptions).ConfigureAwait(false);
    }
}
