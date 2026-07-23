using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Core.Interfaces;

public interface ISettingsService
{
    AppSettings Settings { get; }
    string SettingsFilePath { get; }
    Task LoadAsync();
    Task SaveAsync();
}
