using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.ClipboardPlus.Services;
using WindowsToolbox.Modules.ClipboardPlus.ViewModels;

namespace WindowsToolbox.Modules.ClipboardPlus;

public sealed class ClipboardPlusModule(ISettingsService settingsService) : IToolModule
{
    private ClipboardPlusViewModel? _viewModel;

    public string Id => "clipboard-plus";
    public string DisplayName => "剪贴板+";
    public string EnglishName => "Clipboard+";
    public string Description => "剪贴板历史与快速复制";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "Clipboard";
    public int SortOrder => 400;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["剪贴板", "历史", "复制", "文本", "clipboard", "copy"];
    public string? ResourceDictionaryPath => null;

    public object CreateViewModel() => _viewModel ??= new ClipboardPlusViewModel(
        new ClipboardPlusService(
            new WindowsClipboardAdapter(),
            new ClipboardHistoryStore(),
            new WindowsClipboardSourceResolver(),
            new WindowsClipboardListener(),
            settingsService),
        settingsService);

    public ClipboardPlusViewModel? CurrentViewModel => _viewModel;
}
