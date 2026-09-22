using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.QuickLaunch.Services;
using WindowsToolbox.Modules.QuickLaunch.ViewModels;

namespace WindowsToolbox.Modules.QuickLaunch;

public sealed class QuickLaunchModule : IToolModule, IDisposable
{
    private QuickLaunchViewModel? _viewModel;

    public QuickLaunchModule(ISettingsService settingsService) => _ = settingsService;

    public string Id => "quick-launch";
    public string DisplayName => "快捷启动";
    public string EnglishName => "Quick Launch";
    public string Description => "快速打开常用应用、文件夹、文件和网页";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "Rocket";
    public int SortOrder => 470;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["快捷启动", "快速打开", "应用", "文件夹", "文件", "网页", "launcher", "quick launch", "favorite"];
    public string? ResourceDictionaryPath => null;
    public IQuickLaunchHotkeyService HotkeyService { get; } = new WindowsQuickLaunchHotkeyService();

    public object CreateViewModel()
    {
        if (_viewModel is not null) return _viewModel;
        _viewModel = new QuickLaunchViewModel(
            new QuickLaunchStore(),
            new WindowsQuickLaunchExecutor(),
            new ShellQuickLaunchIconService());
        _viewModel.SetHotkeyStatus(HotkeyService.IsRegistered);
        return _viewModel;
    }

    public QuickLaunchViewModel? CurrentViewModel => _viewModel;

    public void SetHotkeyEnabled(bool enabled)
    {
        bool registered = HotkeyService.Start(enabled);
        _viewModel?.SetHotkeyStatus(registered);
    }

    public void Dispose()
    {
        _viewModel?.Dispose();
        HotkeyService.Dispose();
    }
}
