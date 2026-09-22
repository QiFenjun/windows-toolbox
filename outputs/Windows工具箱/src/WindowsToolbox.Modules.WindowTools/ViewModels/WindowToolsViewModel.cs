using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.WindowTools.Models;
using WindowsToolbox.Modules.WindowTools.Services;

namespace WindowsToolbox.Modules.WindowTools.ViewModels;

public sealed class WindowToolsViewModel : ObservableObject, IDisposable
{
    private readonly IWindowEnumerator _enumerator;
    private readonly IWindowController _controller;
    private readonly IMonitorService _monitorService;
    private readonly IWindowIconService _icons;
    private CancellationTokenSource? _searchCancellation;
    private string _searchText = string.Empty;
    private WindowItemViewModel? _selectedWindow;
    private MonitorSnapshot? _selectedMonitor;
    private WindowLayoutOption _selectedLayout;
    private string _customWidth = "1280";
    private string _customHeight = "720";
    private string _status = "刷新后选择一个普通顶层窗口即可操作。所有尺寸均为窗口外框物理像素。";
    private string _error = string.Empty;

    public WindowToolsViewModel(IWindowEnumerator enumerator, IWindowController controller, IMonitorService monitorService, IWindowIconService icons)
    {
        _enumerator = enumerator;
        _controller = controller;
        _monitorService = monitorService;
        _icons = icons;
        WindowsView = CollectionViewSource.GetDefaultView(Windows);
        WindowsView.Filter = FilterWindow;
        LayoutOptions =
        [
            new(WindowLayoutPreset.LeftHalf, "左半区域"), new(WindowLayoutPreset.RightHalf, "右半区域"),
            new(WindowLayoutPreset.TopLeft, "左上"), new(WindowLayoutPreset.TopRight, "右上"),
            new(WindowLayoutPreset.BottomLeft, "左下"), new(WindowLayoutPreset.BottomRight, "右下"),
            new(WindowLayoutPreset.Center, "居中")
        ];
        _selectedLayout = LayoutOptions[0];
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CaptureForegroundCommand = new AsyncRelayCommand(CaptureForegroundAsync);
        SetTopMostCommand = new AsyncRelayCommand(() => ExecuteAsync(window => _controller.SetTopMost(window, true)), () => CanOperate);
        ClearTopMostCommand = new AsyncRelayCommand(() => ExecuteAsync(window => _controller.SetTopMost(window, false)), () => CanOperate);
        CenterCommand = new AsyncRelayCommand(() => ExecuteAsync(_controller.Center), () => CanOperate);
        ApplyLayoutCommand = new AsyncRelayCommand(() => ExecuteAsync(window => _controller.ApplyLayout(window, SelectedLayout.Value)), () => CanOperate);
        ApplyPreset720Command = new AsyncRelayCommand(() => ResizeAsync(1280, 720), () => CanOperate);
        ApplyPreset900Command = new AsyncRelayCommand(() => ResizeAsync(1600, 900), () => CanOperate);
        ApplyPreset1080Command = new AsyncRelayCommand(() => ResizeAsync(1920, 1080), () => CanOperate);
        ApplyCustomSizeCommand = new AsyncRelayCommand(ApplyCustomSizeAsync, () => CanOperate);
        MoveMonitorCommand = new AsyncRelayCommand(() => ExecuteAsync(window => _controller.MoveToMonitor(window, SelectedMonitor?.Id ?? string.Empty)), () => CanOperate && SelectedMonitor is not null);
        _ = RefreshAsync();
    }

    public ObservableCollection<WindowItemViewModel> Windows { get; } = [];
    public ObservableCollection<MonitorSnapshot> Monitors { get; } = [];
    public ICollectionView WindowsView { get; }
    public IReadOnlyList<WindowLayoutOption> LayoutOptions { get; }
    public AsyncRelayCommand RefreshCommand { get; }
    public AsyncRelayCommand CaptureForegroundCommand { get; }
    public AsyncRelayCommand SetTopMostCommand { get; }
    public AsyncRelayCommand ClearTopMostCommand { get; }
    public AsyncRelayCommand CenterCommand { get; }
    public AsyncRelayCommand ApplyLayoutCommand { get; }
    public AsyncRelayCommand ApplyPreset720Command { get; }
    public AsyncRelayCommand ApplyPreset900Command { get; }
    public AsyncRelayCommand ApplyPreset1080Command { get; }
    public AsyncRelayCommand ApplyCustomSizeCommand { get; }
    public AsyncRelayCommand MoveMonitorCommand { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty)) return;
            _searchCancellation?.Cancel();
            CancellationTokenSource source = _searchCancellation = new();
            _ = RefreshFilterAsync(source.Token);
        }
    }

    public WindowItemViewModel? SelectedWindow
    {
        get => _selectedWindow;
        set
        {
            if (!SetProperty(ref _selectedWindow, value)) return;
            if (value is not null)
                SelectedMonitor = Monitors.FirstOrDefault(monitor => monitor.Id == value.Snapshot.MonitorId) ?? SelectedMonitor;
            OnPropertyChanged(nameof(CanOperate));
            NotifyOperationCommands();
        }
    }

    public MonitorSnapshot? SelectedMonitor
    {
        get => _selectedMonitor;
        set
        {
            if (!SetProperty(ref _selectedMonitor, value)) return;
            NotifyOperationCommands();
        }
    }

    public WindowLayoutOption SelectedLayout { get => _selectedLayout; set => SetProperty(ref _selectedLayout, value); }
    public string CustomWidth { get => _customWidth; set => SetProperty(ref _customWidth, value ?? string.Empty); }
    public string CustomHeight { get => _customHeight; set => SetProperty(ref _customHeight, value ?? string.Empty); }
    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public bool CanOperate => SelectedWindow?.IsModifiable == true;
    public string SelectedInfoText => SelectedWindow is null ? string.Empty : BuildSelectedInfo(SelectedWindow);

    public async Task RefreshAsync()
    {
        nint previous = SelectedWindow?.Hwnd ?? 0;
        IReadOnlyList<WindowSnapshot> snapshots = await Task.Run(_enumerator.Enumerate).ConfigureAwait(true);
        Monitors.Clear();
        foreach (MonitorSnapshot monitor in _monitorService.GetMonitors()) Monitors.Add(monitor);
        Windows.Clear();
        foreach (WindowSnapshot snapshot in snapshots)
        {
            WindowItemViewModel item = new(snapshot);
            Windows.Add(item);
            _ = LoadIconAsync(item);
        }
        WindowsView.Refresh();
        SelectedWindow = previous == 0 ? null : Windows.FirstOrDefault(item => item.Hwnd == previous);
        Status = $"已刷新 {Windows.Count} 个可操作或可查看的顶层窗口。";
        Error = string.Empty;
    }

    public async Task CaptureForegroundAsync()
    {
        Status = "请在 1.5 秒内切换到目标窗口。";
        Error = string.Empty;
        await Task.Delay(1500).ConfigureAwait(true);
        nint foreground = _enumerator.GetForegroundWindow();
        if (foreground == 0) { Error = "未找到当前前台窗口。"; return; }
        await RefreshAsync().ConfigureAwait(true);
        SelectedWindow = Windows.FirstOrDefault(item => item.Hwnd == foreground);
        if (SelectedWindow is null) Error = "当前前台窗口不可操作或不在安全列表中。";
        else { Status = $"已选择：{SelectedWindow.Title}"; Error = string.Empty; }
    }

    public string BuildSelectedInfo() => SelectedWindow is null ? string.Empty : BuildSelectedInfo(SelectedWindow);

    private async Task ResizeAsync(int width, int height) => await ExecuteAsync(window => _controller.Resize(window, width, height)).ConfigureAwait(true);

    private async Task ApplyCustomSizeAsync()
    {
        if (!int.TryParse(CustomWidth, out int width) || width <= 0) { Error = "请输入有效宽度。"; return; }
        if (!int.TryParse(CustomHeight, out int height) || height <= 0) { Error = "请输入有效高度。"; return; }
        await ResizeAsync(width, height).ConfigureAwait(true);
    }

    private async Task ExecuteAsync(Func<WindowSnapshot, WindowOperationResult> operation)
    {
        WindowItemViewModel? selected = SelectedWindow;
        if (selected is null || !selected.IsModifiable) { Error = "请选择可操作的普通窗口。"; return; }
        WindowOperationResult result = await Task.Run(() => operation(selected.Snapshot)).ConfigureAwait(true);
        if (result.Success)
        {
            Status = result.UserMessage;
            Error = string.Empty;
            await RefreshAsync().ConfigureAwait(true);
            SelectedWindow = Windows.FirstOrDefault(item => item.Hwnd == selected.Hwnd);
        }
        else Error = result.UserMessage;
    }

    private async Task RefreshFilterAsync(CancellationToken token)
    {
        try { await Task.Delay(120, token).ConfigureAwait(true); WindowsView.Refresh(); }
        catch (OperationCanceledException) { }
    }

    private bool FilterWindow(object value)
    {
        if (value is not WindowItemViewModel item) return false;
        return WindowSearch.Matches(item.Snapshot, SearchText);
    }

    private async Task LoadIconAsync(WindowItemViewModel item)
    {
        try { item.Icon = await _icons.GetAsync(item.ExecutablePath).ConfigureAwait(true); item.RefreshIcon(); }
        catch { }
    }

    private static string BuildSelectedInfo(WindowItemViewModel item) =>
        $"Title: {item.Title}{Environment.NewLine}Process: {item.ProcessName}{Environment.NewLine}PID: {item.ProcessId}{Environment.NewLine}HWND: {item.HwndText}{Environment.NewLine}Rect: {item.WindowRect}{Environment.NewLine}Monitor: {item.MonitorText}";

    private void NotifyOperationCommands()
    {
        foreach (AsyncRelayCommand command in new[] { SetTopMostCommand, ClearTopMostCommand, CenterCommand, ApplyLayoutCommand, ApplyPreset720Command, ApplyPreset900Command, ApplyPreset1080Command, ApplyCustomSizeCommand, MoveMonitorCommand })
            command.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
    }
}
