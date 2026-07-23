using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.Shutdown.Services;
using WindowsToolbox.Modules.Shutdown.ViewModels;

namespace WindowsToolbox.Modules.Shutdown;

public sealed class ShutdownModule(
    IShutdownService shutdownService,
    ISettingsService settingsService) : IToolModule
{
    private ShutdownViewModel? _viewModel;

    public string Id => "shutdown";
    public string DisplayName => "定时关机";
    public string Description => "创建、查看和取消 Windows 定时关机计划";
    public string Category => "系统工具";
    public string IconKey => "Power";
    public int SortOrder => 100;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["关机", "定时", "倒计时", "shutdown", "系统", "电源"];
    public string ResourceDictionaryPath =>
        "/WindowsToolbox.Modules.Shutdown;component/ModuleResources.xaml";

    public object CreateViewModel() =>
        _viewModel ??= new ShutdownViewModel(shutdownService, settingsService);
}
