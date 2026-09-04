using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.NetworkTraffic.Services;
using WindowsToolbox.Modules.NetworkTraffic.ViewModels;

namespace WindowsToolbox.Modules.NetworkTraffic;

public sealed class NetworkTrafficModule(ISettingsService settingsService) : IToolModule
{
    private NetworkTrafficViewModel? _viewModel;

    public string Id => "network-traffic";
    public string DisplayName => "网络流量";
    public string Description => "实时查看应用程序的上传、下载和网络路径";
    public string Category => "系统工具";
    public string IconKey => "Network";
    public int SortOrder => 300;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } = ["网络", "流量", "上传", "下载", "VPN", "代理", "traffic", "network"];
    public string ResourceDictionaryPath => "/WindowsToolbox.Modules.NetworkTraffic;component/ModuleResources.xaml";

    public object CreateViewModel() => _viewModel ??= new NetworkTrafficViewModel(
        settingsService,
        new NetworkMonitorHelperClient(),
        new NetworkTrafficAggregator(),
        new IpHelperConnectionSnapshotProvider(),
        new NetworkInterfaceSnapshotProvider(),
        new TrafficHistoryStore());

    public NetworkTrafficViewModel? CurrentViewModel => _viewModel;
}
