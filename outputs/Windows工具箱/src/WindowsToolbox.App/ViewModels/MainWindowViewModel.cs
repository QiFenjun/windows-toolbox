using System.Collections.ObjectModel;
using System.IO;
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
        IThemeService themeService)
    {
        _moduleRegistry = moduleRegistry;
        _navigationService = navigationService;
        _settingsService = settingsService;
        _themeService = themeService;
        _isSidebarExpanded = settingsService.Settings.RememberSidebarExpanded
            ? settingsService.Settings.IsSidebarExpanded
            : true;

        foreach (IToolModule module in moduleRegistry.Modules)
        {
            Modules.Add(new ModuleItemViewModel(module));
            navigationService.Register(module.Id, module.CreateViewModel);
        }

        _homeViewModel = new HomeViewModel(moduleRegistry, settingsService, Navigate);
        navigationService.Register("home", () => _homeViewModel);
        navigationService.Register("settings", () => new SettingsViewModel(settingsService, themeService, moduleRegistry));
        navigationService.Register("about", () => new AboutViewModel());
        navigationService.Navigated += OnNavigated;

        NavigateCommand = new RelayCommand<string>(Navigate);
        ToggleSidebarCommand = new RelayCommand(ToggleSidebar);
        ToggleThemeCommand = new RelayCommand(ToggleTheme);
        ClearSearchCommand = new RelayCommand(() => SearchText = string.Empty);
    }

    public ObservableCollection<ModuleItemViewModel> Modules { get; } = [];
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

            SearchResults.Clear();
            if (!string.IsNullOrWhiteSpace(value))
            {
                foreach (IToolModule module in _moduleRegistry.Search(value))
                    SearchResults.Add(new ModuleItemViewModel(module));
            }
            IsSearchOpen = SearchResults.Count > 0;
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
    public RelayCommand<string> NavigateCommand { get; }
    public RelayCommand ToggleSidebarCommand { get; }
    public RelayCommand ToggleThemeCommand { get; }
    public RelayCommand ClearSearchCommand { get; }

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

    private void OnNavigated(object? sender, NavigationChangedEventArgs e)
    {
        CurrentContent = e.ViewModel;
        CurrentPageId = e.PageId;

        foreach (ModuleItemViewModel module in Modules)
            module.IsSelected = string.Equals(module.Id, e.PageId, StringComparison.OrdinalIgnoreCase);

        IToolModule? toolModule = _moduleRegistry.Find(e.PageId);
        if (toolModule is not null)
        {
            CurrentTitle = toolModule.DisplayName;
            CurrentDescription = toolModule.Description;
            RememberRecent(toolModule.Id);
        }
        else
        {
            (CurrentTitle, CurrentDescription) = e.PageId switch
            {
                "settings" => ("设置", "调整主题、启动页面与操作偏好"),
                "about" => ("关于", "查看版本、安全与隐私信息"),
                _ => ("首页", "集中管理常用的 Windows 小工具")
            };
        }
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
}
