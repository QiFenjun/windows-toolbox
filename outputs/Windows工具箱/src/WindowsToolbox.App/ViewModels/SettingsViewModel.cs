using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.App.ViewModels;

public sealed record ThemeOption(ThemeMode Value, string DisplayName);
public sealed record StartupOption(string Id, string DisplayName);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private ThemeOption _selectedTheme;
    private StartupOption _selectedStartupPage;

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IModuleRegistry moduleRegistry)
    {
        _settingsService = settingsService;
        _themeService = themeService;

        ThemeOptions =
        [
            new(ThemeMode.System, "跟随系统"),
            new(ThemeMode.Light, "浅色模式"),
            new(ThemeMode.Dark, "深色模式")
        ];
        StartupOptions =
        [
            new("home", "首页"),
            .. moduleRegistry.Modules
                .Where(module => module.IsAvailable)
                .Select(module => new StartupOption(module.Id, module.DisplayName))
        ];

        _selectedTheme = ThemeOptions.First(option => option.Value == settingsService.Settings.Theme);
        _selectedStartupPage = StartupOptions.FirstOrDefault(
            option => option.Id == settingsService.Settings.StartupPageId) ?? StartupOptions[0];
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; }
    public IReadOnlyList<StartupOption> StartupOptions { get; }

    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value))
            {
                _settingsService.Settings.Theme = value.Value;
                _themeService.Apply(value.Value);
            }
        }
    }

    public StartupOption SelectedStartupPage
    {
        get => _selectedStartupPage;
        set
        {
            if (SetProperty(ref _selectedStartupPage, value))
                _settingsService.Settings.StartupPageId = value.Id;
        }
    }

    public bool ConfirmOperations
    {
        get => _settingsService.Settings.ConfirmOperations;
        set
        {
            if (_settingsService.Settings.ConfirmOperations == value)
                return;
            _settingsService.Settings.ConfirmOperations = value;
            OnPropertyChanged();
        }
    }

    public bool RememberSidebarExpanded
    {
        get => _settingsService.Settings.RememberSidebarExpanded;
        set
        {
            if (_settingsService.Settings.RememberSidebarExpanded == value)
                return;
            _settingsService.Settings.RememberSidebarExpanded = value;
            OnPropertyChanged();
        }
    }

    public string SaveStatus { get; private set; } = string.Empty;
    public AsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        try
        {
            await _settingsService.SaveAsync();
            SaveStatus = "设置已保存";
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            SaveStatus = "无法保存设置，请检查用户目录权限。";
        }
        OnPropertyChanged(nameof(SaveStatus));
    }
}
