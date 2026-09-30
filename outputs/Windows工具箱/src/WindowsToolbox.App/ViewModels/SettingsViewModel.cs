using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.App.Services;

namespace WindowsToolbox.App.ViewModels;

public sealed record ThemeOption(ThemeMode Value, string DisplayName);
public sealed record StartupOption(string Id, string DisplayName);
public sealed record MotionOption(ReducedMotionMode Value, string DisplayName);

public sealed class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly WindowsStartupRegistrationService _startupRegistrationService;
    private readonly IMotionService _motionService;
    private readonly LocalizationService _localization;
    private readonly IModuleRegistry _moduleRegistry;
    private readonly Action<bool>? _quickLaunchHotkeyChanged;
    private ThemeOption _selectedTheme;
    private StartupOption _selectedStartupPage;
    private MotionOption _selectedMotion;
    private string? _saveStatusKey;

    public SettingsViewModel(
        ISettingsService settingsService,
        IThemeService themeService,
        IModuleRegistry moduleRegistry,
        WindowsStartupRegistrationService startupRegistrationService,
        IMotionService motionService,
        LocalizationService localization,
        Action<bool>? quickLaunchHotkeyChanged = null)
    {
        _settingsService = settingsService;
        _themeService = themeService;
        _startupRegistrationService = startupRegistrationService;
        _motionService = motionService;
        _localization = localization;
        _moduleRegistry = moduleRegistry;
        _quickLaunchHotkeyChanged = quickLaunchHotkeyChanged;

        ThemeOptions = BuildThemeOptions();
        StartupOptions = BuildStartupOptions();
        MotionOptions = BuildMotionOptions();

        _selectedTheme = ThemeOptions.First(option => option.Value == settingsService.Settings.Theme);
        _selectedStartupPage = StartupOptions.FirstOrDefault(
            option => option.Id == settingsService.Settings.StartupPageId) ?? StartupOptions[0];
        _selectedMotion = MotionOptions.FirstOrDefault(
            option => option.Value == settingsService.Settings.ReducedMotion) ?? MotionOptions[0];
        SaveCommand = new AsyncRelayCommand(SaveAsync);
        _localization.LanguageChanged += Localization_LanguageChanged;
    }

    public IReadOnlyList<ThemeOption> ThemeOptions { get; private set; }
    public IReadOnlyList<StartupOption> StartupOptions { get; private set; }
    public IReadOnlyList<MotionOption> MotionOptions { get; private set; }

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

    public MotionOption SelectedMotion
    {
        get => _selectedMotion;
        set
        {
            if (SetProperty(ref _selectedMotion, value))
            {
                _settingsService.Settings.ReducedMotion = value.Value;
                _motionService.SetMode(value.Value);
            }
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

    public bool NetworkTrafficAutoStart
    {
        get => _settingsService.Settings.NetworkTrafficAutoStart;
        set
        {
            if (_settingsService.Settings.NetworkTrafficAutoStart == value)
                return;
            _settingsService.Settings.NetworkTrafficAutoStart = value;
            OnPropertyChanged();
        }
    }

    public bool NetworkTrafficContinueInBackground
    {
        get => _settingsService.Settings.NetworkTrafficContinueInBackground;
        set
        {
            if (_settingsService.Settings.NetworkTrafficContinueInBackground == value)
                return;
            _settingsService.Settings.NetworkTrafficContinueInBackground = value;
            OnPropertyChanged();
        }
    }

    public bool NetworkTrafficStartWithWindows
    {
        get => _settingsService.Settings.NetworkTrafficStartWithWindows;
        set
        {
            if (_settingsService.Settings.NetworkTrafficStartWithWindows == value)
                return;
            _settingsService.Settings.NetworkTrafficStartWithWindows = value;
            OnPropertyChanged();
        }
    }

    public bool ClipboardPlusEnabled
    {
        get => _settingsService.Settings.ClipboardPlusEnabled;
        set
        {
            if (_settingsService.Settings.ClipboardPlusEnabled == value) return;
            _settingsService.Settings.ClipboardPlusEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool ClipboardPlusHotkeyEnabled
    {
        get => _settingsService.Settings.ClipboardPlusHotkeyEnabled;
        set
        {
            if (_settingsService.Settings.ClipboardPlusHotkeyEnabled == value) return;
            _settingsService.Settings.ClipboardPlusHotkeyEnabled = value;
            OnPropertyChanged();
        }
    }

    public bool QuickLaunchHotkeyEnabled
    {
        get => _settingsService.Settings.QuickLaunchHotkeyEnabled;
        set
        {
            if (_settingsService.Settings.QuickLaunchHotkeyEnabled == value) return;
            _settingsService.Settings.QuickLaunchHotkeyEnabled = value;
            _quickLaunchHotkeyChanged?.Invoke(value);
            OnPropertyChanged();
        }
    }

    public string SaveStatus { get; private set; } = string.Empty;
    public AsyncRelayCommand SaveCommand { get; }

    private IReadOnlyList<ThemeOption> BuildThemeOptions() =>
    [
        new(ThemeMode.System, _localization.GetString("ThemeSystem")),
        new(ThemeMode.Light, _localization.GetString("ThemeLight")),
        new(ThemeMode.Dark, _localization.GetString("ThemeDark"))
    ];

    private IReadOnlyList<StartupOption> BuildStartupOptions() =>
    [
        new("home", _localization.GetString("HomeTitle")),
        .. _moduleRegistry.Modules.Where(module => module.IsAvailable)
            .Select(module => new StartupOption(module.Id,
                _localization.IsEnglish ? module.EnglishName : module.DisplayName))
    ];

    private IReadOnlyList<MotionOption> BuildMotionOptions() =>
    [
        new(ReducedMotionMode.Full, _localization.GetString("MotionFull")),
        new(ReducedMotionMode.Reduced, _localization.GetString("MotionReduced")),
        new(ReducedMotionMode.Off, _localization.GetString("MotionOff"))
    ];

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        ThemeMode theme = _selectedTheme.Value;
        string startup = _selectedStartupPage.Id;
        ReducedMotionMode motion = _selectedMotion.Value;
        ThemeOptions = BuildThemeOptions();
        StartupOptions = BuildStartupOptions();
        MotionOptions = BuildMotionOptions();
        _selectedTheme = ThemeOptions.First(option => option.Value == theme);
        _selectedStartupPage = StartupOptions.FirstOrDefault(option => option.Id == startup) ?? StartupOptions[0];
        _selectedMotion = MotionOptions.First(option => option.Value == motion);
        OnPropertyChanged(nameof(ThemeOptions));
        OnPropertyChanged(nameof(StartupOptions));
        OnPropertyChanged(nameof(MotionOptions));
        OnPropertyChanged(nameof(SelectedTheme));
        OnPropertyChanged(nameof(SelectedStartupPage));
        OnPropertyChanged(nameof(SelectedMotion));
        if (_saveStatusKey is not null)
        {
            SaveStatus = _localization.GetString(_saveStatusKey);
            OnPropertyChanged(nameof(SaveStatus));
        }
    }

    private async Task SaveAsync()
    {
        try
        {
            await _settingsService.SaveAsync();
            _startupRegistrationService.Apply(NetworkTrafficStartWithWindows);
            _saveStatusKey = "SettingsSaved";
        }
        catch (Exception exception) when (
            exception is IOException or
            UnauthorizedAccessException or
            InvalidOperationException or
            System.Security.SecurityException)
        {
            _saveStatusKey = "SettingsSaveFailed";
        }
        SaveStatus = _localization.GetString(_saveStatusKey);
        OnPropertyChanged(nameof(SaveStatus));
    }
}
