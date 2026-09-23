using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.Utilities.ViewModels;

namespace WindowsToolbox.Modules.Utilities;

public sealed class UtilitiesModule : IToolModule, IDisposable
{
    private UtilitiesViewModel? _viewModel;

    public string Id => "utilities";
    public string DisplayName => "小工具";
    public string EnglishName => "Utilities";
    public string Description => "二维码、颜色等轻量实用工具集合";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "GridView";
    public int SortOrder => 480;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } = ["二维码", "QR", "颜色", "Color", "取色", "二维码识别"];
    public string? ResourceDictionaryPath => null;

    public object CreateViewModel() => _viewModel ??= new UtilitiesViewModel();

    public void Dispose() => _viewModel?.Dispose();
}
