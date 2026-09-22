using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.KeepAwake.Models;
using WindowsToolbox.Modules.KeepAwake.Services;

namespace WindowsToolbox.Modules.KeepAwake.ViewModels;

public sealed class KeepAwakeViewModel : ObservableObject
{
    private readonly KeepAwakeService _service;
    private readonly ISettingsService _settings;
    private int _selectedMode;
    private DurationOption _duration;
    private KeepAwakeSnapshot _snapshot;
    private string _persistenceError = "";
    public IReadOnlyList<string> Modes { get; } = ["保持电脑唤醒 / Keep system awake", "保持电脑和屏幕唤醒 / Keep system and display awake"];
    public IReadOnlyList<DurationOption> Durations { get; } = [new(15,"15 分钟 / 15 minutes"),new(30,"30 分钟 / 30 minutes"),new(60,"1 小时 / 1 hour"),new(120,"2 小时 / 2 hours"),new(240,"4 小时 / 4 hours"),new(0,"直到手动停止 / Until stopped")];
    public KeepAwakeViewModel(KeepAwakeService service, ISettingsService settings)
    {
        _service=service; _settings=settings; _snapshot=service.Snapshot;
        _selectedMode = settings.Settings.KeepAwakeLastMode is 0 or 1 ? settings.Settings.KeepAwakeLastMode : 0;
        _duration = Durations.FirstOrDefault(d=>d.Minutes==settings.Settings.KeepAwakeLastDurationMinutes) ?? Durations[1];
        StartCommand=new(StartAsync,()=>CanConfigure); StopCommand=new(StopAsync,()=>!CanConfigure);
    }
    public int SelectedMode { get=>_selectedMode; set { if(CanConfigure && value is 0 or 1) SetProperty(ref _selectedMode,value); } }
    public DurationOption SelectedDuration { get=>_duration; set { if(CanConfigure && Durations.Contains(value)) SetProperty(ref _duration,value); } }
    public bool CanConfigure => !_snapshot.IsBusy;
    public bool IsActive => _snapshot.State==KeepAwakeState.Active;
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public string RemainingText => _snapshot.IsBusy ? FormatRemaining(_snapshot.Remaining) : "—";
    public string TimingText => _snapshot.StartedAt is null ? "" : $"开始 / Started {_snapshot.StartedAt.Value.ToLocalTime():HH:mm:ss}" + (_snapshot.ExpiresAt is null ? "" : $" · 预计结束 / Expected end {_snapshot.ExpiresAt.Value.ToLocalTime():HH:mm:ss}");
    public string Status => (_snapshot.State switch
    {
        KeepAwakeState.Active => "● 正在保持唤醒 / Awake request active",
        KeepAwakeState.Stopping => "正在停止 / Stopping…",
        _ => _snapshot.EndReason switch
        {
            KeepAwakeEndReason.Expired => "保持唤醒已结束 / Keep Awake expired",
            KeepAwakeEndReason.ActivationFailed => $"无法启用保持唤醒 / Activation failed · Win32 {_snapshot.Win32Error} (0=unavailable)",
            KeepAwakeEndReason.ReleaseFailed => $"电源请求释放失败，持有线程已退出 / Release failed; owner thread exited · Win32 {_snapshot.Win32Error}",
            _ => "未开启 / Inactive"
        }
    }) + _persistenceError;
    public const string Boundaries="请求 Windows 在启用期间保持系统唤醒；不修改电源计划、不模拟输入。仍可锁屏或主动休眠；关键电源/电池和管理员策略仍可能让系统休眠。关闭窗口后在托盘继续；真正退出时释放。\nRequests Windows to stay awake temporarily. Locking, manual sleep and system power policies still apply. Continues in tray; releases on app exit.";

    public async Task StartAsync()
    {
        if (!CanConfigure) return;
        _settings.Settings.KeepAwakeLastMode=SelectedMode;
        _settings.Settings.KeepAwakeLastDurationMinutes=SelectedDuration.Minutes;
        _persistenceError="";
        Task<bool> start=_service.StartAsync((KeepAwakeMode)SelectedMode,SelectedDuration.Minutes==0?null:TimeSpan.FromMinutes(SelectedDuration.Minutes));
        Refresh(); await start; Refresh();
        try { await _settings.SaveAsync(); }
        catch(Exception ex) when (ex is IOException or UnauthorizedAccessException) { _persistenceError="\n偏好未保存 / Preferences not saved"; Refresh(); }
    }
    public async Task StopAsync() { Task stop=_service.StopAsync(); Refresh(); await stop; Refresh(); }
    public void Refresh()
    {
        _snapshot=_service.Snapshot;
        foreach(string property in new[]{nameof(CanConfigure),nameof(IsActive),nameof(Status),nameof(RemainingText),nameof(TimingText)}) OnPropertyChanged(property);
        StartCommand.NotifyCanExecuteChanged(); StopCommand.NotifyCanExecuteChanged();
    }
    public static string FormatRemaining(TimeSpan? remaining) => remaining is null ? "直到手动停止 / Until stopped" :
        $"{(int)Math.Max(0,remaining.Value.TotalHours):00}:{Math.Max(0,remaining.Value.Minutes):00}:{Math.Max(0,remaining.Value.Seconds):00}";
}
