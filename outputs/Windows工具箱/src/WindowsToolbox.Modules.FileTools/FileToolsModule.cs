using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.FileTools.Services;
using WindowsToolbox.Modules.FileTools.ViewModels;

namespace WindowsToolbox.Modules.FileTools;

public sealed class FileToolsModule : IToolModule
{
    private FileToolsViewModel? _viewModel;

    public string Id => "file-tools";
    public string DisplayName => "File Tools";
    public string Description => "文件重命名、校验与路径处理";
    public string Category => "效率工具";
    public string IconKey => "Folder";
    public int SortOrder => 460;
    public bool IsAvailable => OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords { get; } =
        ["文件", "重命名", "Hash", "校验", "路径", "文件信息", "文件夹大小", "file", "rename"];
    public string? ResourceDictionaryPath => null;

    public object CreateViewModel() => _viewModel ??= new FileToolsViewModel(
        new RenameService(),
        new HashService(),
        new PathToolsService(),
        new FileInfoService());
}
