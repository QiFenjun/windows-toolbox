using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.TextTools.Services;
using WindowsToolbox.Modules.TextTools.ViewModels;

namespace WindowsToolbox.Modules.TextTools;

public sealed class TextToolsModule : IToolModule
{
    private TextToolsViewModel? _viewModel;

    public string Id => "text-tools";
    public string DisplayName => "文本工具";
    public string EnglishName => "Text Tools";
    public string Description => "文本转换与快速处理";
    public string Category => "效率工具";
    public string EnglishCategory => "Productivity Tools";
    public string IconKey => "Edit";
    public int SortOrder => 450;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["文本", "转换", "清理", "查找", "替换", "JSON", "URL", "Base64", "Unicode", "text"];
    public string? ResourceDictionaryPath => null;

    public object CreateViewModel() => _viewModel ??= new TextToolsViewModel(new WindowsTextClipboardAdapter());
}
