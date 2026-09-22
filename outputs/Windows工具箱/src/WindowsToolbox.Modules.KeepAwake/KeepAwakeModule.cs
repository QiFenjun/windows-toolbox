using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.KeepAwake.Interop;
using WindowsToolbox.Modules.KeepAwake.Services;
using WindowsToolbox.Modules.KeepAwake.ViewModels;

namespace WindowsToolbox.Modules.KeepAwake;

public sealed class KeepAwakeModule : IToolModule, IDisposable
{
    private readonly ISettingsService _settings;
    private KeepAwakeViewModel? _viewModel;
    public KeepAwakeService Service { get; }
    public KeepAwakeModule(ISettingsService settings, KeepAwakeService? service = null) { _settings=settings; Service=service??new(new WindowsExecutionStatePlatform()); }
    public string Id=>"keep-awake";
    public string DisplayName=>"保持唤醒";
    public string EnglishName=>"Keep Awake";
    public string Description=>"临时保持电脑或显示器处于唤醒状态";
    public string Category=>"效率工具";
    public string EnglishCategory=>"Productivity Tools";
    public string IconKey=>"Theme";
    public int SortOrder=>500;
    public bool IsAvailable=>OperatingSystem.IsWindows();
    public IReadOnlyList<string> Keywords {get;}=["唤醒","休眠","屏幕","awake","sleep","display"];
    public string? ResourceDictionaryPath=>null;
    public bool KeepInTray=>Service.Snapshot.IsBusy;
    public object CreateViewModel()=>_viewModel??=new(Service,_settings);
    public void Dispose()=>Service.Dispose();
}
