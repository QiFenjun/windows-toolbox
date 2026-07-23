using System.Windows.Threading;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Shutdown.Models;
using WindowsToolbox.Modules.Shutdown.Services;

namespace WindowsToolbox.Modules.Shutdown.ViewModels;

public sealed class ShutdownViewModel : ObservableObject
{
    private readonly IShutdownService _shutdownService;
    private readonly ISettingsService _settingsService;
    private readonly DispatcherTimer _timer;
    private DateTime? _selectedDate;
    private int _selectedHour;
    private int _selectedMinute;
    private bool _isConfirmationVisible;
    private string _confirmationTitle = string.Empty;
    private string _confirmationMessage = string.Empty;
    private string _notificationMessage = string.Empty;
    private string _notificationKind = "Info";
    private PendingAction _pendingAction;

    public ShutdownViewModel(IShutdownService shutdownService, ISettingsService settingsService)
    {
        _shutdownService = shutdownService;
        _settingsService = settingsService;

        DateTime initial = RoundToMinute(DateTime.Now.AddHours(1));
        _selectedDate = initial.Date;
        _selectedHour = initial.Hour;
        _selectedMinute = initial.Minute;

        PrepareQuickCommand = new RelayCommand<string>(PrepareQuick);
        RequestScheduleCommand = new RelayCommand(RequestSchedule);
        RequestCancelCommand = new RelayCommand(RequestCancel);
        ConfirmCommand = new AsyncRelayCommand(ConfirmPendingActionAsync);
        DismissConfirmationCommand = new RelayCommand(DismissConfirmation);
        DismissNotificationCommand = new RelayCommand(() => NotificationMessage = string.Empty);

        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += (_, _) => RefreshStatus();
        _timer.Start();
        RefreshStatus();
    }

    public IReadOnlyList<int> Hours { get; } = Enumerable.Range(0, 24).ToArray();
    public IReadOnlyList<int> Minutes { get; } = Enumerable.Range(0, 60).ToArray();

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
                OnPropertyChanged(nameof(SelectedShutdownTime));
        }
    }

    public int SelectedHour
    {
        get => _selectedHour;
        set
        {
            if (SetProperty(ref _selectedHour, value))
                OnPropertyChanged(nameof(SelectedShutdownTime));
        }
    }

    public int SelectedMinute
    {
        get => _selectedMinute;
        set
        {
            if (SetProperty(ref _selectedMinute, value))
                OnPropertyChanged(nameof(SelectedShutdownTime));
        }
    }

    public DateTime SelectedShutdownTime =>
        (SelectedDate ?? DateTime.Today).Date.AddHours(SelectedHour).AddMinutes(SelectedMinute);

    public bool HasActivePlan => _shutdownService.GetRemainingTime() is not null;
    public string StatusTitle => HasActivePlan ? "关机计划已生效" : "当前没有待执行的关机计划";
    public string StatusDescription => HasActivePlan
        ? $"预计关机：{_shutdownService.ScheduledTime:yyyy年MM月dd日 HH:mm}"
        : "设置完成后，即使关闭本软件，Windows 仍会按时执行。";
    public string RemainingText => FormatRemaining(_shutdownService.GetRemainingTime());
    public string StatusIconKey => HasActivePlan ? "Timer" : "Power";

    public bool IsConfirmationVisible
    {
        get => _isConfirmationVisible;
        set => SetProperty(ref _isConfirmationVisible, value);
    }

    public string ConfirmationTitle
    {
        get => _confirmationTitle;
        set => SetProperty(ref _confirmationTitle, value);
    }

    public string ConfirmationMessage
    {
        get => _confirmationMessage;
        set => SetProperty(ref _confirmationMessage, value);
    }

    public string NotificationMessage
    {
        get => _notificationMessage;
        set
        {
            if (SetProperty(ref _notificationMessage, value))
                OnPropertyChanged(nameof(HasNotification));
        }
    }

    public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationMessage);

    public string NotificationKind
    {
        get => _notificationKind;
        set => SetProperty(ref _notificationKind, value);
    }

    public RelayCommand<string> PrepareQuickCommand { get; }
    public RelayCommand RequestScheduleCommand { get; }
    public RelayCommand RequestCancelCommand { get; }
    public AsyncRelayCommand ConfirmCommand { get; }
    public RelayCommand DismissConfirmationCommand { get; }
    public RelayCommand DismissNotificationCommand { get; }

    private void PrepareQuick(string? option)
    {
        DateTime target = option switch
        {
            "30" => RoundToMinute(DateTime.Now.AddMinutes(30)),
            "60" => RoundToMinute(DateTime.Now.AddHours(1)),
            "120" => RoundToMinute(DateTime.Now.AddHours(2)),
            "tonight" => GetTonightAtEleven(),
            _ => RoundToMinute(DateTime.Now.AddHours(1))
        };

        SelectedDate = target.Date;
        SelectedHour = target.Hour;
        SelectedMinute = target.Minute;
        ShowNotification($"已选择 {target:MM月dd日 HH:mm}，确认时间后点击“设置关机”。", "Info");
    }

    private void RequestSchedule()
    {
        ShutdownOperationResult validation = _shutdownService.ValidateShutdownTime(SelectedShutdownTime);
        if (!validation.IsSuccess)
        {
            ShowNotification(validation.Message, "Error");
            return;
        }

        _pendingAction = PendingAction.Schedule;
        if (!_settingsService.Settings.ConfirmOperations)
        {
            ConfirmCommand.Execute(null);
            return;
        }

        ConfirmationTitle = "确认设置关机计划";
        ConfirmationMessage =
            $"Windows 将在 {SelectedShutdownTime:yyyy年MM月dd日 HH:mm} 关机。\n请提前保存正在编辑的文件。";
        IsConfirmationVisible = true;
    }

    private void RequestCancel()
    {
        _pendingAction = PendingAction.Cancel;
        if (!_settingsService.Settings.ConfirmOperations)
        {
            ConfirmCommand.Execute(null);
            return;
        }

        ConfirmationTitle = "取消关机计划";
        ConfirmationMessage = "确定取消当前 Windows 关机计划吗？";
        IsConfirmationVisible = true;
    }

    private async Task ConfirmPendingActionAsync()
    {
        IsConfirmationVisible = false;
        ShutdownOperationResult result;
        try
        {
            result = _pendingAction == PendingAction.Cancel
                ? await _shutdownService.CancelShutdownAsync()
                : await _shutdownService.ScheduleShutdownAsync(SelectedShutdownTime);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        ShowNotification(result.Message, result.IsSuccess ? "Success" : "Error");
        RefreshStatus();
    }

    private void DismissConfirmation()
    {
        IsConfirmationVisible = false;
        _pendingAction = PendingAction.None;
    }

    private void RefreshStatus()
    {
        OnPropertyChanged(nameof(HasActivePlan));
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusDescription));
        OnPropertyChanged(nameof(RemainingText));
        OnPropertyChanged(nameof(StatusIconKey));
    }

    private void ShowNotification(string message, string kind)
    {
        NotificationKind = kind;
        NotificationMessage = message;
    }

    private static DateTime RoundToMinute(DateTime value) =>
        new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0);

    private static DateTime GetTonightAtEleven()
    {
        DateTime target = DateTime.Today.AddHours(23);
        return target <= DateTime.Now.AddMinutes(1) ? target.AddDays(1) : target;
    }

    private static string FormatRemaining(TimeSpan? remaining)
    {
        if (remaining is null)
            return "尚未设置";

        TimeSpan value = remaining.Value;
        if (value.TotalDays >= 1)
            return $"{(int)value.TotalDays} 天 {value.Hours:00} 小时 {value.Minutes:00} 分";
        if (value.TotalHours >= 1)
            return $"{(int)value.TotalHours:00} 小时 {value.Minutes:00} 分 {value.Seconds:00} 秒";
        return $"{Math.Max(0, value.Minutes):00} 分 {Math.Max(0, value.Seconds):00} 秒";
    }

    private enum PendingAction
    {
        None,
        Schedule,
        Cancel
    }
}
