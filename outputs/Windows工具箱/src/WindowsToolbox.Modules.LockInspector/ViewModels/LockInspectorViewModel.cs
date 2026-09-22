using System.Collections.ObjectModel;
using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.LockInspector.Models;
using WindowsToolbox.Modules.LockInspector.Services;

namespace WindowsToolbox.Modules.LockInspector.ViewModels;

public enum LockScanState { Idle, Scanning, Found, NoBlocker, Cancelled, Failed, Limited }

public sealed class LockInspectorViewModel(LockScanService service) : ObservableObject, IDisposable
{
    private CancellationTokenSource? _cancellation;
    private Task _scan = Task.CompletedTask;
    private int _generation;
    private bool _disposed;
    private LockScanRequest? _request;
    private LockScanState _state;
    private string _status = "选择文件，或拖入文件/文件夹。 / Choose or drop files/folders.";
    private string _progress = "";
    private LockingProcessInfo? _selected;
    private bool _recursive;
    public ObservableCollection<LockingProcessInfo> Blockers { get; } = [];
    public IReadOnlyList<DriveTarget> Drives { get; } = DriveTarget.GetReadyDrives();
    public DriveTarget? SelectedDrive { get; set; }
    public bool Recursive { get => _recursive; set => SetProperty(ref _recursive, value); }
    public bool IsScanning => State == LockScanState.Scanning;
    public bool CanSelect => !_disposed && !IsScanning;
    public bool CanRescan => CanSelect && _request is not null;
    public bool CanCopy => Selected is not null;
    public bool CanOpenLocation => ProcessDetailsService.CanOpenLocation(Selected);
    public string TargetText => _request is null ? "未选择目标 / No target" : string.Join(Environment.NewLine, _request.Targets);
    public string ModeText => _request?.ScanType switch { LockScanType.Folder => "文件夹 / Folder · Best-effort", LockScanType.Drive => "驱动器 / Drive · Best-effort", _ => "文件 / Files · Restart Manager" };
    public LockScanState State { get => _state; private set { SetProperty(ref _state,value); OnPropertyChanged(nameof(IsScanning)); OnPropertyChanged(nameof(CanSelect)); OnPropertyChanged(nameof(CanRescan)); } }
    public string Status { get => _status; private set => SetProperty(ref _status,value); }
    public string ProgressText { get => _progress; private set => SetProperty(ref _progress,value); }
    public LockScanResult? Result { get; private set; }
    public LockingProcessInfo? Selected { get => _selected; set { SetProperty(ref _selected,value); OnPropertyChanged(nameof(CanCopy)); OnPropertyChanged(nameof(CanOpenLocation)); } }
    public const string Boundaries = "仅诊断，不结束程序、不提权、不弹出设备。文件夹/驱动器通过枚举文件扫描，可能遗漏目录、卷、设备或驱动程序句柄。\nDiagnosis only. Folder/drive scans may miss directory, volume, device or driver handles.";

    public Task ScanAsync(LockScanRequest request)
    {
        if (_disposed) return Task.CompletedTask;
        int generation = ++_generation;
        _cancellation?.Cancel();
        Task previous = _scan;
        _scan = RunAsync(previous, request with { Targets = request.Targets.ToArray() }, generation);
        return _scan;
    }

    private async Task RunAsync(Task previous, LockScanRequest request, int generation)
    {
        await previous;
        if (_disposed || generation != _generation) return;
        using CancellationTokenSource cancellation = new();
        _cancellation = cancellation;
        _request = request; OnPropertyChanged(nameof(TargetText)); OnPropertyChanged(nameof(ModeText));
        Blockers.Clear(); Selected = null; Result = null;
        State = LockScanState.Scanning; Status = "正在扫描… / Scanning…";
        try
        {
            Progress<LockScanProgress> progress = new(p =>
            {
                if (generation == _generation && IsScanning)
                    ProgressText = Counts(p);
            });
            LockScanResult result = await service.ScanAsync(request, cancellation.Token, progress);
            if (_disposed || generation != _generation) return;
            // Only metadata, never target file contents. Failures to read a process are non-fatal.
            LockingProcessInfo[] details = await Task.Run(() => result.Blockers
                .Select(info => cancellation.IsCancellationRequested ? info : ProcessDetailsService.Refresh(info)).ToArray());
            if (_disposed || generation != _generation) return;
            if (cancellation.IsCancellationRequested) result = result with { WasCancelled = true };
            Result = result;
            foreach (LockingProcessInfo info in details) Blockers.Add(info);
            Selected = Blockers.FirstOrDefault();
            ProgressText = Counts(new(result.FilesEnumerated,result.FilesRegistered,result.FilesSkipped,result.Batches,result.Blockers.Count));
            State = result.WasCancelled ? LockScanState.Cancelled : result.Errors.Count > 0 ? LockScanState.Failed :
                result.WasLimited || result.FilesSkipped > 0 ? LockScanState.Limited : result.Blockers.Count > 0 ? LockScanState.Found : LockScanState.NoBlocker;
            Status = Describe(result);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            if (generation == _generation && !_disposed) { State = LockScanState.Failed; Status = "当前方法无法完成检测。 / Detection failed; retry."; }
        }
        finally { if (ReferenceEquals(_cancellation,cancellation)) _cancellation = null; }
    }

    public Task RescanAsync() => _request is null ? Task.CompletedTask : ScanAsync(_request with { Recursive = Recursive });
    public void Cancel() => _cancellation?.Cancel();
    public void ShowActionError() => Status = "无法完成操作；进程可能已退出或路径不可用。 / Action unavailable; process or path may have changed.";
    public static string Describe(LockScanResult result)
    {
        string status = result.WasCancelled ? "扫描已取消，保留已扫描结果。 / Cancelled; partial results." :
            result.Errors.Count > 0 ? $"{RestartManagerErrors.Describe(result.RestartManagerError)} (Code {result.RestartManagerError})" :
            result.Blockers.Count > 0 ? "已发现与当前资源集合相关的应用或服务。 / Processes associated with the scanned resource set." :
            "Restart Manager 未在已扫描文件中发现占用应用或服务。 / No blockers found in scanned files.";
        if (result.WasLimited) status += "\n已达到扫描上限，结果可能不完整。 / Scan limit reached; incomplete results.";
        if (result.FilesSkipped > 0) status += "\n已跳过部分文件或目录，结果可能不完整。 / Some entries were skipped; incomplete results.";
        if (result.Target.ScanType != LockScanType.Files) status += "\nWindows 仍可能因目录、卷、设备或驱动程序句柄阻止安全移除。 / Other handles may still prevent safe removal.";
        return status;
    }
    private static string Counts(LockScanProgress p) => $"枚举 / Enumerated {p.FilesEnumerated} · 注册 / Registered {p.FilesRegistered} · 批次 / Batches {p.Batches} · 跳过 / Skipped {p.FilesSkipped} · 占用者 / Blockers {p.Blockers}";
    public void Dispose() { _disposed = true; ++_generation; Cancel(); Blockers.Clear(); Selected = null; _request = null; Result = null; }
}
