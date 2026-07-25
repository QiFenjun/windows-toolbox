using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.InstalledApps.Services;
using WindowsToolbox.Modules.InstalledApps.ViewModels;

namespace WindowsToolbox.Modules.InstalledApps;

public sealed class InstalledAppsModule : IToolModule
{
    private InstalledAppsViewModel? _viewModel;

    public string Id => "installed-apps";
    public string DisplayName => "应用管理";
    public string Description => "查看、筛选和安全调用已安装软件的卸载程序";
    public string Category => "系统工具";
    public string IconKey => "Apps";
    public int SortOrder => 200;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["应用", "软件", "安装", "卸载", "大小", "installed", "apps"];
    public string ResourceDictionaryPath =>
        "/WindowsToolbox.Modules.InstalledApps;component/ModuleResources.xaml";

    public object CreateViewModel() =>
        _viewModel ??= CreateInstalledAppsViewModel();

    private static InstalledAppsViewModel CreateInstalledAppsViewModel()
    {
        InstalledAppService appService = new(
            [new RegistryInstalledAppProvider()]);
        ApplicationSizeService sizeService = new(new ApplicationSizeCache());
        return new InstalledAppsViewModel(
            appService,
            sizeService,
            new ApplicationActionService(),
            new ApplicationClipboardService());
    }
}
