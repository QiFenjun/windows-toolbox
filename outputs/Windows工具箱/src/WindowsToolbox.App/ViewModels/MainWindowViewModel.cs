using System.Collections.ObjectModel;
using System.IO;
using WindowsToolbox.App.Services;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.App.ViewModels;

public sealed class MainWindowViewModel : ObservableObject
{
    private readonly IModuleRegistry _moduleRegistry;
    private readonly INavigationService _navigationService;
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;
    private readonly IMotionService _motionService;
    private readonly LocalizationService _localization;
    private readonly HomeViewModel _homeViewModel;
    private object? _currentContent;
    private string _currentPageId = "home";
    private string _currentTitle = "首页";
    private string _currentDescription = "集中管理常用的 Windows 小工具";
    private string _searchText = string.Empty;
    private bool _isSidebarExpanded;
    private bool _isSearchOpen;

    public MainWindowViewModel(
        IModuleRegistry moduleRegistry,
        INavigationService navigationService,
        ISettingsService settingsService,
        IThemeService themeService,
        IMotionService motionService,
        LocalizationService localization,
        Action<bool>? quickLaunchHotkeyChanged = null)
    {
        _moduleRegistry = moduleRegistry;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _motionService = motionService;
        _localization = localization;
        _isSidebarExpanded = settingsService.Settings.RememberSidebarExpanded
            ? settingsService.Settings.IsSidebarExpanded
            : true;

        foreach (IToolModule module in moduleRegistry.Modules)
        {
            ModuleItemViewModel item = new(module, localization);
            if (string.Equals(module.Category, "效率工具", StringComparison.OrdinalIgnoreCase))
                EfficiencyModules.Add(item);
            else
                Modules.Add(item);
            navigationService.Register(module.Id, module.CreateViewModel);
        }

        _homeViewModel = new HomeViewModel(moduleRegistry, settingsService, Navigate, motionService, localization);
        navigationService.Register("home", () => _homeViewModel);
        navigationService.Register("settings", () => new SettingsViewModel(
            settingsService,
            themeService,
            moduleRegistry,
            new WindowsStartupRegistrationService(),
            motionService,
            localization,
            quickLaunchHotkeyChanged));
        navigationService.Register("about", () => new AboutViewModel());
        navigationService.Navigated += OnNavigated;

        NavigateCommand = new RelayCommand<string>(Navigate);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        SetLanguageCommand = new RelayCommand<string>(SetLanguage);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
        _localization.LanguageChanged += Localization_LanguageChanged;
    }

    public ObservableCollection<ModuleItemViewModel> Modules { get; } = [];
    public ObservableCollection<ModuleItemViewModel> EfficiencyModules { get; } = [];
    public ObservableCollection<ModuleItemViewModel> SearchResults { get; } = [];

    public object? CurrentContent
    {
        get => _currentContent;
        private set => SetProperty(ref _currentContent, value);
    }

    public string CurrentPageId
    {
        get => _currentPageId;
        private set => SetProperty(ref _currentPageId, value);
    }

    public string CurrentTitle
    {
        get => _currentTitle;
        private set => SetProperty(ref _currentTitle, value);
    }

    public string CurrentDescription
    {
        get => _currentDescription;
        private set => SetProperty(ref _currentDescription, value);
    }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value))
                return;

            RefreshSearchResults();
        }
    }

    public bool IsSearchOpen
    {
        get => _isSearchOpen;
        private set => SetProperty(ref _isSearchOpen, value);
    }

    public bool IsSidebarExpanded
    {
        get => _isSidebarExpanded;
        private set
        {
            if (SetProperty(ref _isSidebarExpanded, value))
                OnPropertyChanged(nameof(IsSidebarCollapsed));
        }
    }

    public bool IsSidebarCollapsed => !IsSidebarExpanded;
    public ReducedMotionMode ReducedMotion => _motionService.CurrentMode;
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand<string> SetLanguageCommand { get; }
    public RelayCommand ClearSearchCommand { get; }
    public string LanguageBadge => _localization.IsEnglish ? "EN" : "中";
    public string LanguageToolTip => _localization.GetString("LanguageTooltip");
    public string ChineseLanguageMenuLabel => _localization.IsEnglish ? "简体中文" : "✓  简体中文";
    public string EnglishLanguageMenuLabel => _localization.IsEnglish ? "✓  English" : "English";
    public bool IsChineseLanguage => !_localization.IsEnglish;
    public bool IsEnglishLanguage => _localization.IsEnglish;

    public void Start()
    {
        string startupPage = _settingsService.Settings.StartupPageId;
        if (!_navigationService.Navigate(startupPage))
            _navigationService.Navigate("home");
    }

    private void Navigate(string? pageId)
    {
        if (string.IsNullOrWhiteSpace(pageId))
            return;

        _navigationService.Navigate(pageId);
        SearchText = string.Empty;
    }

    public void NavigateTo(string pageId) => Navigate(pageId);

    private void OnNavigated(object? sender, NavigationChangedEventArgs e)
    {
        CurrentContent = e.ViewModel;
        CurrentPageId = e.PageId;

        foreach (ModuleItemViewModel module in Modules.Concat(EfficiencyModules))
            module.IsSelected = string.Equals(module.Id, e.PageId, StringComparison.OrdinalIgnoreCase);

        IToolModule? toolModule = _moduleRegistry.Find(e.PageId);
        if (toolModule is not null)
        {
            UpdateHeader(toolModule);
            RememberRecent(toolModule.Id);
        }
        else
            UpdateHeader(e.PageId);
    }

    private void UpdateHeader(IToolModule module)
    {
        CurrentTitle = _localization.IsEnglish ? module.EnglishName : module.DisplayName;
        CurrentDescription = _localization.GetModuleDescription(module);
    }

    private void UpdateHeader(string pageId)
    {
        (string chinese, string english, string descriptionKey) = pageId switch
        {
            "settings" => ("设置", "Settings", "SettingsDescription"),
            "about" => ("关于", "About", "AboutDescription"),
            _ => ("首页", "Home", "HomeDescription")
        };
        CurrentTitle = _localization.IsEnglish ? english : chinese;
        CurrentDescription = _localization.GetString(descriptionKey);
    }

    private void Localization_LanguageChanged(object? sender, EventArgs e)
    {
        foreach (ModuleItemViewModel module in Modules.Concat(EfficiencyModules))
            module.RefreshLocalization();
        _homeViewModel.RefreshLocalization();
        RefreshSearchResults();
        if (_moduleRegistry.Find(CurrentPageId) is IToolModule currentModule)
            UpdateHeader(currentModule);
        else
            UpdateHeader(CurrentPageId);
        OnPropertyChanged(nameof(LanguageBadge));
        OnPropertyChanged(nameof(LanguageToolTip));
        OnPropertyChanged(nameof(ChineseLanguageMenuLabel));
        OnPropertyChanged(nameof(EnglishLanguageMenuLabel));
        OnPropertyChanged(nameof(IsChineseLanguage));
        OnPropertyChanged(nameof(IsEnglishLanguage));
    }

    private void RefreshSearchResults()
    {
        SearchResults.Clear();
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            foreach (IToolModule module in _moduleRegistry.Search(SearchText))
                SearchResults.Add(new ModuleItemViewModel(module, _localization));
        }
        IsSearchOpen = SearchResults.Count > 0;
    }

    private void RememberRecent(string moduleId)
    {
        List<string> recent = _settingsService.Settings.RecentModuleIds;
        recent.RemoveAll(id => string.Equals(id, moduleId, StringComparison.OrdinalIgnoreCase));
        recent.Insert(0, moduleId);
        if (recent.Count > 5)
            recent.RemoveRange(5, recent.Count - 5);
        _homeViewModel.Refresh();
        _ = SaveSettingsQuietlyAsync();
    }

    private async Task SaveSettingsQuietlyAsync()
    {
        try
        {
            await _settingsService.SaveAsync();
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException)
        {
            // 最近使用记录失败不影响主流程。
        }
    }

    private void ToggleSidebar()
    {
        IsSidebarExpanded = !IsSidebarExpanded;
        if (_settingsService.Settings.RememberSidebarExpanded)
        {
            _settingsService.Settings.IsSidebarExpanded = IsSidebarExpanded;
            _ = SaveSettingsQuietlyAsync();
        }
    }

    private void ToggleTheme()
    {
        ThemeMode next = _themeService.CurrentMode == ThemeMode.Dark
            ? ThemeMode.Light
            : ThemeMode.Dark;
        _settingsService.Settings.Theme = next;
        _themeService.Apply(next);
        _ = SaveSettingsQuietlyAsync();
    }

    private void SetLanguage(string? language)
    {
        if (language is not (LocalizationService.Chinese or LocalizationService.English) ||
            string.Equals(_localization.CurrentLanguage, language, StringComparison.Ordinal))
            return;

        _settingsService.Settings.Language = language;
        _localization.Apply(language);
        _ = SaveSettingsQuietlyAsync();
    }
}
