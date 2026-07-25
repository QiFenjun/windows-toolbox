using System.Collections.ObjectModel;
using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.InstalledApps.Models;
using WindowsToolbox.Modules.InstalledApps.Services;
using WindowsToolbox.Modules.InstalledApps.Utilities;

namespace WindowsToolbox.Modules.InstalledApps.ViewModels;

public sealed class InstalledAppsViewModel : ObservableObject
{
    private readonly InstalledAppService _installedAppService;
    private readonly ApplicationSizeService _sizeService;
    private readonly IApplicationActionService _actionService;
    private readonly IApplicationClipboardService _clipboardService;
    private readonly List<InstalledApplication> _allApplications = [];
    private CancellationTokenSource? _refreshCancellation;
    private CancellationTokenSource? _scanCancellation;
    private CancellationTokenSource? _searchCancellation;
    private InstalledApplication? _selectedApplication;
    private string _searchText = string.Empty;
    private string _selectedSortMode = "名称";
    private string _selectedPublisher = "全部发布者";
    private string _selectedSource = "全部来源";
    private bool _showSystemComponents;
    private bool _isLoading;
    private bool _isScanning;
    private bool _hasNotification;
    private string _notificationMessage = string.Empty;
    private string _notificationKind = "Info";
    private string _scanProgressText = string.Empty;
    private bool _isUninstallConfirmationVisible;
    private bool _isUninstalling;

    public InstalledAppsViewModel(
        InstalledAppService installedAppService,
        ApplicationSizeService sizeService,
        IApplicationActionService actionService,
        IApplicationClipboardService clipboardService)
    {
        _installedAppService = installedAppService;
        _sizeService = sizeService;
        _actionService = actionService;
        _clipboardService = clipboardService;

        SortModes = ["名称", "大小（从大到小）", "安装日期（从新到旧）", "发布者"];
        SourceFilters = ["全部来源", "注册表", "Microsoft Store / MSIX"];
        PublisherFilters.Add("全部发布者");

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsLoading);
        CancelRefreshCommand = new RelayCommand(CancelRefresh, () => IsLoading);
        ScanSizeCommand = new AsyncRelayCommand(
            ScanSelectedSizeAsync,
            () => SelectedApplication?.CanScan == true && !IsScanning);
        CancelScanCommand = new RelayCommand(CancelScan, () => IsScanning);
        OpenLocationCommand = new RelayCommand(
            OpenSelectedLocation,
            () => SelectedApplication?.CanScan == true);
        CopyInformationCommand = new RelayCommand(
            CopySelectedInformation,
            () => SelectedApplication is not null);
        RequestUninstallCommand = new RelayCommand(
            RequestUninstall,
            () => SelectedApplication?.CanUninstall == true && !IsUninstalling);
        CancelUninstallCommand = new RelayCommand(CancelUninstall);
        ConfirmUninstallCommand = new AsyncRelayCommand(
            ConfirmUninstallAsync,
            () => SelectedApplication?.CanUninstall == true && !IsUninstalling);
        DismissNotificationCommand = new RelayCommand(() => HasNotification = false);

        _ = RefreshAsync();
    }

    public ObservableCollection<InstalledApplication> VisibleApplications { get; } = [];
    public ObservableCollection<string> PublisherFilters { get; } = [];
    public IReadOnlyList<string> SortModes { get; }
    public IReadOnlyList<string> SourceFilters { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
                DebounceFilters();
        }
    }

    public string SelectedSortMode
    {
        get => _selectedSortMode;
        set
        {
            if (SetProperty(ref _selectedSortMode, value))
                ApplyFilters();
        }
    }

    public string SelectedPublisher
    {
        get => _selectedPublisher;
        set
        {
            if (SetProperty(ref _selectedPublisher, value))
                ApplyFilters();
        }
    }

    public string SelectedSource
    {
        get => _selectedSource;
        set
        {
            if (SetProperty(ref _selectedSource, value))
                ApplyFilters();
        }
    }

    public bool ShowSystemComponents
    {
        get => _showSystemComponents;
        set
        {
            if (SetProperty(ref _showSystemComponents, value))
                ApplyFilters();
        }
    }

    public InstalledApplication? SelectedApplication
    {
        get => _selectedApplication;
        set
        {
            if (!SetProperty(ref _selectedApplication, value))
                return;

            IsUninstallConfirmationVisible = false;
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(UninstallRiskText));
            NotifyCommandStates();
        }
    }

    public bool HasSelection => SelectedApplication is not null;

    public bool IsLoading
    {
        get => _isLoading;
        private set
        {
            if (!SetProperty(ref _isLoading, value))
                return;
            RefreshCommand.NotifyCanExecuteChanged();
            CancelRefreshCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value))
                return;
            ScanSizeCommand.NotifyCanExecuteChanged();
            CancelScanCommand.NotifyCanExecuteChanged();
        }
    }

    public bool IsUninstalling
    {
        get => _isUninstalling;
        private set
        {
            if (!SetProperty(ref _isUninstalling, value))
                return;
            RequestUninstallCommand.NotifyCanExecuteChanged();
            ConfirmUninstallCommand.NotifyCanExecuteChanged();
        }
    }

    public bool HasNotification
    {
        get => _hasNotification;
        private set => SetProperty(ref _hasNotification, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        private set => SetProperty(ref _notificationMessage, value);
    }

    public string NotificationKind
    {
        get => _notificationKind;
        private set => SetProperty(ref _notificationKind, value);
    }

    public string ScanProgressText
    {
        get => _scanProgressText;
        private set => SetProperty(ref _scanProgressText, value);
    }

    public bool IsUninstallConfirmationVisible
    {
        get => _isUninstallConfirmationVisible;
        private set => SetProperty(ref _isUninstallConfirmationVisible, value);
    }

    public int TotalCount => _allApplications.Count;
    public int VisibleCount => VisibleApplications.Count;
    public int KnownSizeCount => _allApplications.Count(app => app.DisplaySizeBytes.HasValue);
    public int UnknownSizeCount => TotalCount - KnownSizeCount;
    public long KnownSizeTotalBytes => _allApplications.Sum(app => app.DisplaySizeBytes ?? 0L);
    public string KnownSizeTotalText => SizeFormatter.Format(KnownSizeTotalBytes);
    public string LoadingText => IsLoading ? "正在读取已安装软件…" : string.Empty;
    public string UninstallRiskText => SelectedApplication?.IsHighRisk == true
        ? "该条目可能是系统组件、驱动、运行库或 Microsoft 关键组件。请确认用途后再继续。"
        : "卸载将由该软件自身登记的卸载程序完成，不会自动删除残留文件或注册表项。";

    public AsyncRelayCommand RefreshCommand { get; }
    public RelayCommand CancelRefreshCommand { get; }
    public AsyncRelayCommand ScanSizeCommand { get; }
    public RelayCommand CancelScanCommand { get; }
    public RelayCommand OpenLocationCommand { get; }
    public RelayCommand CopyInformationCommand { get; }
    public RelayCommand RequestUninstallCommand { get; }
    public RelayCommand CancelUninstallCommand { get; }
    public AsyncRelayCommand ConfirmUninstallCommand { get; }
    public RelayCommand DismissNotificationCommand { get; }

    public async Task RefreshAsync()
    {
        _refreshCancellation?.Cancel();
        _refreshCancellation?.Dispose();
        _refreshCancellation = new CancellationTokenSource();
        CancellationToken token = _refreshCancellation.Token;
        IsLoading = true;
        HasNotification = false;

        try
        {
            IReadOnlyList<InstalledApplication> applications =
                await _installedAppService.LoadAsync(token);

            foreach (InstalledApplication application in applications)
            {
                token.ThrowIfCancellationRequested();
                await _sizeService.ApplyCachedSizeAsync(application, token);
            }

            _allApplications.Clear();
            _allApplications.AddRange(applications);
            RebuildPublisherFilters();
            ApplyFilters();
            ShowNotification($"已加载 {_allApplications.Count} 个软件条目。", "Success");
        }
        catch (OperationCanceledException)
        {
            ShowNotification("已取消刷新。", "Info");
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is InvalidOperationException)
        {
            ShowNotification("无法完成软件列表读取，请稍后重试。", "Error");
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(LoadingText));
        }
    }

    public void ApplyFilters()
    {
        IReadOnlyList<InstalledApplication> query = InstalledAppQuery.Apply(
            _allApplications,
            SearchText,
            SelectedSortMode,
            SelectedPublisher,
            SelectedSource,
            ShowSystemComponents);

        string? selectedId = SelectedApplication?.Id;
        VisibleApplications.Clear();
        foreach (InstalledApplication application in query)
            VisibleApplications.Add(application);

        SelectedApplication = selectedId is null
            ? VisibleApplications.FirstOrDefault()
            : VisibleApplications.FirstOrDefault(app =>
                string.Equals(app.Id, selectedId, StringComparison.OrdinalIgnoreCase));

        OnPropertyChanged(nameof(VisibleCount));
        NotifySummaryChanged();
    }

    private async Task ScanSelectedSizeAsync()
    {
        InstalledApplication? application = SelectedApplication;
        if (application?.CanScan != true)
            return;

        _scanCancellation?.Cancel();
        _scanCancellation?.Dispose();
        _scanCancellation = new CancellationTokenSource();
        IsScanning = true;
        application.IsScanning = true;
        ScanProgressText = "正在计算…";

        try
        {
            Progress<SizeScanProgress> progress = new(value =>
                ScanProgressText = $"已扫描 {value.FileCount:N0} 个文件，{SizeFormatter.Format(value.SizeBytes)}");
            ApplicationSizeInfo result = await _sizeService.ScanAsync(
                application,
                progress,
                _scanCancellation.Token);
            application.ScannedSizeBytes = result.SizeBytes;
            application.ScannedAt = result.ScannedAt;
            ApplyFilters();
            ShowNotification(
                $"目录扫描完成：{SizeFormatter.Format(result.SizeBytes)}，跳过 {result.SkippedEntryCount:N0} 个不可访问或链接条目。",
                "Success");
        }
        catch (OperationCanceledException)
        {
            ShowNotification("已取消目录大小扫描。", "Info");
        }
        catch (Exception exception) when (
            exception is IOException ||
            exception is UnauthorizedAccessException ||
            exception is DirectoryNotFoundException)
        {
            ShowNotification("无法完成目录扫描；安装位置可能不存在或无权访问。", "Error");
        }
        finally
        {
            application.IsScanning = false;
            IsScanning = false;
            ScanProgressText = string.Empty;
        }
    }

    private void OpenSelectedLocation()
    {
        if (SelectedApplication is null)
            return;

        if (!_actionService.OpenInstallLocation(SelectedApplication, out string error))
            ShowNotification(error, "Error");
    }

    private void CopySelectedInformation()
    {
        InstalledApplication? app = SelectedApplication;
        if (app is null)
            return;

        string information = string.Join(
            Environment.NewLine,
            $"名称：{app.DisplayName}",
            $"版本：{app.VersionText}",
            $"发布者：{app.PublisherText}",
            $"安装日期：{app.InstallDateText}",
            $"占用大小：{app.SizeText}（{app.SizeSourceText}）",
            $"安装位置：{(string.IsNullOrWhiteSpace(app.InstallLocation) ? "未知" : app.InstallLocation)}",
            $"来源：{app.SourceText}",
            $"架构：{app.Architecture}");
        try
        {
            _clipboardService.CopyText(information);
            ShowNotification("软件信息已复制到剪贴板。", "Success");
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.ExternalException ||
            exception is InvalidOperationException)
        {
            ShowNotification("剪贴板暂时不可用，请稍后重试。", "Error");
        }
    }

    private void RequestUninstall()
    {
        if (SelectedApplication?.CanUninstall != true)
            return;
        IsUninstallConfirmationVisible = true;
        OnPropertyChanged(nameof(UninstallRiskText));
    }

    private void CancelUninstall() =>
        IsUninstallConfirmationVisible = false;

    private async Task ConfirmUninstallAsync()
    {
        InstalledApplication? application = SelectedApplication;
        if (application?.CanUninstall != true)
            return;

        IsUninstalling = true;
        IsUninstallConfirmationVisible = false;
        string applicationId = application.Id;
        try
        {
            using CancellationTokenSource waitLimit = new(TimeSpan.FromHours(2));
            UninstallResult result = await _actionService.UninstallAsync(application, waitLimit.Token);
            if (!result.Started)
            {
                ShowNotification(result.Message, "Error");
                return;
            }

            await RefreshAsync();
            bool stillPresent = _allApplications.Any(app =>
                string.Equals(app.Id, applicationId, StringComparison.OrdinalIgnoreCase));
            ShowNotification(
                stillPresent
                    ? "卸载程序已结束，但软件仍被检测到；它可能需要重启或手动完成。"
                    : "卸载完成，软件条目已从列表中消失。",
                stillPresent ? "Info" : "Success");
        }
        finally
        {
            IsUninstalling = false;
        }
    }

    private void CancelRefresh() => _refreshCancellation?.Cancel();
    private void CancelScan() => _scanCancellation?.Cancel();

    private void DebounceFilters()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
        _searchCancellation = new CancellationTokenSource();
        CancellationToken token = _searchCancellation.Token;
        _ = ApplyFiltersAfterDelayAsync(token);
    }

    private async Task ApplyFiltersAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(250, cancellationToken);
            ApplyFilters();
        }
        catch (OperationCanceledException)
        {
            // 后续输入会启动新的筛选。
        }
    }

    private void RebuildPublisherFilters()
    {
        string previous = SelectedPublisher;
        PublisherFilters.Clear();
        PublisherFilters.Add("全部发布者");
        foreach (string publisher in _allApplications
                     .Select(app => app.PublisherText)
                     .Distinct(StringComparer.CurrentCultureIgnoreCase)
                     .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase))
        {
            PublisherFilters.Add(publisher);
        }

        SelectedPublisher = PublisherFilters.Contains(previous) ? previous : "全部发布者";
    }

    private void NotifySummaryChanged()
    {
        OnPropertyChanged(nameof(TotalCount));
        OnPropertyChanged(nameof(KnownSizeCount));
        OnPropertyChanged(nameof(UnknownSizeCount));
        OnPropertyChanged(nameof(KnownSizeTotalBytes));
        OnPropertyChanged(nameof(KnownSizeTotalText));
    }

    private void NotifyCommandStates()
    {
        ScanSizeCommand.NotifyCanExecuteChanged();
        OpenLocationCommand.NotifyCanExecuteChanged();
        CopyInformationCommand.NotifyCanExecuteChanged();
        RequestUninstallCommand.NotifyCanExecuteChanged();
        ConfirmUninstallCommand.NotifyCanExecuteChanged();
    }

    private void ShowNotification(string message, string kind)
    {
        NotificationMessage = message;
        NotificationKind = kind;
        HasNotification = true;
    }
}
