using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.WindowTools.Services;
using WindowsToolbox.Modules.WindowTools.ViewModels;

namespace WindowsToolbox.Modules.WindowTools;

public sealed class WindowToolsModule : IToolModule
{
    private WindowToolsViewModel? _viewModel;

    public string Id => "window-tools";
    public string DisplayName => "窗口工具";
    public string EnglishName => "Window Tools";
    public string Description => "窗口置顶、定位、尺寸调整与信息查看";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "Window";
    public int SortOrder => 480;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["窗口", "置顶", "居中", "显示器", "尺寸", "位置", "window", "topmost", "monitor", "resize"];
    public string? ResourceDictionaryPath => null;

    public object CreateViewModel()
    {
        if (_viewModel is not null) return _viewModel;
        WindowsWindowPlatform platform = new();
        WindowSafetyPolicy safety = new();
        MonitorService monitors = new(platform);
        _viewModel = new WindowToolsViewModel(
            new WindowEnumerator(platform, safety),
            new WindowController(platform, monitors, safety),
            monitors,
            new ShellWindowIconService());
        return _viewModel;
    }
}
