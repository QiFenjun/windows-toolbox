using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.LockInspector.Services;
using WindowsToolbox.Modules.LockInspector.ViewModels;

namespace WindowsToolbox.Modules.LockInspector;

public sealed class LockInspectorModule : IToolModule, IDisposable
{
    private LockInspectorViewModel? _viewModel;
    public string Id => "lock-inspector";
    public string DisplayName => "占用检测";
    public string EnglishName => "Lock Inspector";
    public string Description => "检测文件被哪些应用或服务占用";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "Search";
    public int SortOrder => 490;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } = ["占用", "文件", "锁", "lock", "file", "inspector"];
    public string? ResourceDictionaryPath => null;
    public object CreateViewModel() => _viewModel ??= new(new LockScanService(new RestartManagerClient()));
    public void Dispose() => _viewModel?.Dispose();
}
