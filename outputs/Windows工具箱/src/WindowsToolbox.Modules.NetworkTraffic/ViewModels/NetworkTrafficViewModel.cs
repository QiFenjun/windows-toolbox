using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Threading;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.NetworkTraffic.Models;
using WindowsToolbox.Modules.NetworkTraffic.Services;

namespace WindowsToolbox.Modules.NetworkTraffic.ViewModels;

public sealed class NetworkTrafficViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly NetworkMonitorHelperClient _helperClient;
    private readonly NetworkTrafficAggregator _aggregator;
    private readonly IpHelperConnectionSnapshotProvider _connectionsProvider;
    private readonly NetworkInterfaceSnapshotProvider _interfacesProvider;
    private readonly TrafficHistoryStore _historyStore;
    private readonly DispatcherTimer _refreshTimer;
    private readonly Dispatcher _dispatcher;
    private readonly FixedRingBuffer<(double Download, double Upload)> _history = new(60);
    private IReadOnlyDictionary<string, DailyTrafficRecord> _todayHistory = new Dictionary<string, DailyTrafficRecord>();
    private IReadOnlyList<NetworkConnectionSnapshot> _connections = [];
    private ApplicationTrafficGroup? _selectedApplication;
    private string _searchText = string.Empty;
    private string _selectedFilter = "全部";
    private string _selectedSortMode = "下载速度";
    private string _statusText = "尚未启动高级实时监控。";
    private bool _isMonitoring;
    private bool _isLoading;
    private DateTimeOffset _lastHistorySave = DateTimeOffset.MinValue;

    public NetworkTrafficViewModel(
        ISettingsService settingsService,
        NetworkMonitorHelperClient helperClient,
        NetworkTrafficAggregator aggregator,
        IpHelperConnectionSnapshotProvider connectionsProvider,
        NetworkInterfaceSnapshotProvider interfacesProvider,
        TrafficHistoryStore historyStore)
    {
        _settingsService = settingsService;
        _helperClient = helperClient;
        _aggregator = aggregator;
        _connectionsProvider = connectionsProvider;
        _interfacesProvider = interfacesProvider;
        _historyStore = historyStore;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _helperClient.TrafficReceived += (_, trafficEvent) => _aggregator.Add(trafficEvent);
        _helperClient.StatusChanged += (_, message) => PublishOnUi(() => StatusText = message);
        _helperClient.Stopped += (_, _) => PublishOnUi(() => IsMonitoring = false);
        Filters = ["全部", "活动中", "直连", "VPN", "代理", "系统"];
        SortModes = ["下载速度", "上传速度", "总流量", "连接数", "名称"];
        StartCommand = new AsyncRelayCommand(StartAsync, () => !IsMonitoring && !IsLoading);
        StopCommand = new AsyncRelayCommand(StopAsync, () => IsMonitoring);
        RefreshCommand = new RelayCommand(RefreshSnapshot);
        _refreshTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(1) };
        _refreshTimer.Tick += (_, _) => RefreshSnapshot();
        _refreshTimer.Start();
        _ = InitializeAsync();
    }

    public ObservableCollection<ApplicationTrafficGroup> Applications { get; } = [];
    public ObservableCollection<NetworkInterfaceSnapshot> Interfaces { get; } = [];
    public IReadOnlyList<string> Filters { get; }
    public IReadOnlyList<string> SortModes { get; }
    public PointCollection DownloadPoints { get; } = [];
    public PointCollection UploadPoints { get; } = [];
    public AsyncRelayCommand StartCommand { get; }
    public AsyncRelayCommand StopCommand { get; }
    public RelayCommand RefreshCommand { get; }

    public bool IsMonitoring
    {
        get => _isMonitoring;
        private set
        {
            if (SetProperty(ref _isMonitoring, value))
            {
                StartCommand.NotifyCanExecuteChanged();
                StopCommand.NotifyCanExecuteChanged();
                OnPropertyChanged(nameof(MonitoringStateText));
            }
        }
    }

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (SetProperty(ref _isLoading, value))
                StartCommand.NotifyCanExecuteChanged();
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshSnapshot(); }
    }

    public string SelectedFilter
    {
        get => _selectedFilter;
        set { if (SetProperty(ref _selectedFilter, value)) RefreshSnapshot(); }
    }

    public string SelectedSortMode
    {
        get => _selectedSortMode;
        set { if (SetProperty(ref _selectedSortMode, value)) RefreshSnapshot(); }
    }

    public ApplicationTrafficGroup? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (SetProperty(ref _selectedApplication, value))
                OnPropertyChanged(nameof(SelectedConnections));
        }
    }

    public IReadOnlyList<NetworkConnectionSnapshot> SelectedConnections =>
        SelectedApplication?.Processes.SelectMany(process => process.Connections).ToArray() ?? [];
    public double TotalDownloadRate => Applications.Sum(item => item.DownloadBytesPerSecond);
    public double TotalUploadRate => Applications.Sum(item => item.UploadBytesPerSecond);
    public int ActiveApplicationCount => Applications.Count(item => item.DownloadBytesPerSecond + item.UploadBytesPerSecond > 0);
    public string TotalDownloadRateText => TrafficDisplayFormatter.Rate(TotalDownloadRate);
    public string TotalUploadRateText => TrafficDisplayFormatter.Rate(TotalUploadRate);
    public string MonitoringStateText => IsMonitoring ? "高级 ETW 监控运行中" : "高级 ETW 监控未运行";
    public string PrivacyText => "统计完全在本机完成；不会抓取或保存通信正文、URL、Cookie，也不会解密 HTTPS。";

    private async Task InitializeAsync()
    {
        IsLoading = true;
        try
        {
            _todayHistory = await _historyStore.LoadTodayAsync(CancellationToken.None);
            RefreshSnapshot();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            StatusText = "历史流量记录暂时不可用；实时监控仍可正常启动。";
        }
        finally
        {
            IsLoading = false;
        }

        if (_settingsService.Settings.NetworkTrafficAutoStart)
            await StartAsync();
    }

    private async Task StartAsync()
    {
        IsLoading = true;
        try
        {
            IsMonitoring = await _helperClient.StartAsync(CancellationToken.None);
            if (IsMonitoring)
                StatusText = "高级实时流量监控已启动。应用统计与接口统计分别显示，不会相加。";
        }
        finally { IsLoading = false; }
    }

    private async Task StopAsync()
    {
        await _helperClient.StopAsync();
        IsMonitoring = false;
        StatusText = "网络监控已停止；本次累计数据保留到关闭程序。";
        await PersistTodayAsync();
    }

    public Task StopAsyncForExitAsync() => StopAsync();
    public Task StartAsyncForBackgroundAsync() => StartAsync();

    private void RefreshSnapshot()
    {
        try
        {
            _connections = _connectionsProvider.Read();
            IReadOnlyList<NetworkInterfaceSnapshot> interfaceSnapshots = _interfacesProvider.Read();
            IReadOnlyList<ApplicationTrafficGroup> groups = _aggregator.Snapshot(_connections, interfaceSnapshots, SelectedSortMode, SelectedFilter, SearchText);
            foreach (ApplicationTrafficGroup group in groups)
            {
                _todayHistory.TryGetValue(group.Id, out DailyTrafficRecord? existing);
                group.TodayDownloadBytes = (existing?.DownloadBytes ?? 0) + group.SessionDownloadBytes;
                group.TodayUploadBytes = (existing?.UploadBytes ?? 0) + group.SessionUploadBytes;
            }
            string? selectedId = SelectedApplication?.Id;
            Applications.Clear();
            foreach (ApplicationTrafficGroup group in groups)
                Applications.Add(group);
            SelectedApplication = selectedId is null
                ? Applications.FirstOrDefault()
                : Applications.FirstOrDefault(item => item.Id == selectedId);

            Interfaces.Clear();
            foreach (NetworkInterfaceSnapshot networkInterface in interfaceSnapshots)
                Interfaces.Add(networkInterface);
            _history.Add((TotalDownloadRate, TotalUploadRate));
            UpdateGraph();
            OnPropertyChanged(nameof(TotalDownloadRate));
            OnPropertyChanged(nameof(TotalUploadRate));
            OnPropertyChanged(nameof(TotalDownloadRateText));
            OnPropertyChanged(nameof(TotalUploadRateText));
            OnPropertyChanged(nameof(ActiveApplicationCount));
            OnPropertyChanged(nameof(SelectedConnections));

            if (DateTimeOffset.UtcNow - _lastHistorySave > TimeSpan.FromSeconds(30))
            {
                _lastHistorySave = DateTimeOffset.UtcNow;
                _ = PersistTodayAsync();
            }
        }
        catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            StatusText = "网络接口或连接快照暂时不可用；将自动重试。";
        }
    }

    private void UpdateGraph()
    {
        (double Download, double Upload)[] values = _history.Snapshot().ToArray();
        double max = Math.Max(1, values.Select(value => Math.Max(value.Download, value.Upload)).DefaultIfEmpty(1).Max());
        DownloadPoints.Clear();
        UploadPoints.Clear();
        for (int index = 0; index < values.Length; index++)
        {
            double x = values.Length <= 1 ? 0 : index * 560d / 59d;
            DownloadPoints.Add(new System.Windows.Point(x, 62 - values[index].Download / max * 56));
            UploadPoints.Add(new System.Windows.Point(x, 62 - values[index].Upload / max * 56));
        }
    }

    private async Task PersistTodayAsync()
    {
        string date = DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd");
        List<DailyTrafficRecord> records = Applications.Select(group =>
        {
            _todayHistory.TryGetValue(group.Id, out DailyTrafficRecord? existing);
            return new DailyTrafficRecord(
                date,
                group.Id,
                group.DisplayName,
                group.Id,
                (existing?.DownloadBytes ?? 0) + group.SessionDownloadBytes,
                (existing?.UploadBytes ?? 0) + group.SessionUploadBytes,
                group.IdentityStatus);
        }).ToList();
        await _historyStore.SaveTodayAsync(records, CancellationToken.None);
    }

    private void PublishOnUi(Action action)
    {
        if (_dispatcher.CheckAccess())
            action();
        else
            _dispatcher.BeginInvoke(action);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _ = _helperClient.DisposeAsync();
    }
}
