using System.Globalization;
using System.Windows.Threading;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.Time.Models;
using WindowsToolbox.Modules.Utilities.Time.Services;

namespace WindowsToolbox.Modules.Utilities.Time.ViewModels;

public sealed class TimeToolsViewModel : ObservableObject, IDisposable
{
    private readonly IClock _clock;
    private readonly IUtilitiesTextClipboardAdapter _clipboard;
    private readonly ITimeRefreshTimer _timer;
    private DateTimeOffset _currentUtc;
    private string _timestampText = "0";
    private int _timestampUnitIndex;
    private string _timestampStatus = string.Empty;
    private string _convertedUtc = string.Empty;
    private string _convertedLocal = string.Empty;
    private string _convertedIso = string.Empty;
    private string _unixSeconds = string.Empty;
    private string _unixMilliseconds = string.Empty;
    private string _dateTimeInput = string.Empty;
    private string _dateTimeStatus = string.Empty;
    private string _sourceZoneSearch = string.Empty;
    private string _targetZoneSearch = string.Empty;
    private TimeZoneInfo _sourceTimeZone;
    private TimeZoneInfo _targetTimeZone = TimeZoneInfo.Utc;
    private string _timeZoneInput = string.Empty;
    private string _timeZoneStatus = string.Empty;
    private string _timeZoneTarget = string.Empty;
    private string _timeZoneUtc = string.Empty;
    private string _timeZoneIso = string.Empty;
    private string _startInput = string.Empty;
    private string _endInput = string.Empty;
    private string _durationStatus = string.Empty;
    private string _durationDisplay = string.Empty;
    private string _durationDays = string.Empty;
    private string _durationHours = string.Empty;
    private string _durationMinutes = string.Empty;
    private string _durationSeconds = string.Empty;
    private string _durationTotalSeconds = string.Empty;
    private string _durationTotalMilliseconds = string.Empty;
    private string _copyStatus = string.Empty;
    private IReadOnlyList<TimeSpan> _dateTimeAmbiguousOffsets = Array.Empty<TimeSpan>();
    private IReadOnlyList<TimeSpan> _timeZoneAmbiguousOffsets = Array.Empty<TimeSpan>();
    private TimeSpan? _selectedDateTimeAmbiguousOffset;
    private TimeSpan? _selectedTimeZoneAmbiguousOffset;
    private IReadOnlyList<TimeZoneInfo> _sourceTimeZones = Array.Empty<TimeZoneInfo>();
    private IReadOnlyList<TimeZoneInfo> _targetTimeZones = Array.Empty<TimeZoneInfo>();
    private bool _active;
    private bool _disposed;

    public static IReadOnlyList<string> TimestampUnits { get; } = Array.AsReadOnly(new[] { "Seconds", "Milliseconds" });

    public TimeToolsViewModel()
        : this(new SystemClock(), new DispatcherTimeRefreshTimerFactory(Dispatcher.CurrentDispatcher),
            new WindowsUtilitiesTextClipboardAdapter()) { }

    public TimeToolsViewModel(IClock clock, ITimeRefreshTimerFactory timerFactory, IUtilitiesTextClipboardAdapter clipboard)
    {
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _clipboard = clipboard ?? throw new ArgumentNullException(nameof(clipboard));
        ArgumentNullException.ThrowIfNull(timerFactory);
        _sourceTimeZone = TimeZoneCatalog.SystemZones.FirstOrDefault(zone => zone.Id == TimeZoneInfo.Local.Id) ?? TimeZoneInfo.Local;
        _timer = timerFactory.Create(TimeSpan.FromSeconds(1), RefreshCurrentTime);
        RefreshSourceTimeZones();
        RefreshTargetTimeZones();
        TimestampUnitIndex = 0;
        TimeZoneInput = _clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);
        DateTimeInput = TimeZoneInput;
        StartInput = TimeZoneInput;
        EndInput = TimeConversionService.FormatIso(_clock.UtcNow);
        CopyTextCommand = new RelayCommand<string>(CopyText, text => !string.IsNullOrEmpty(text));
        ConvertTimestampCommand = new RelayCommand(ConvertTimestamp);
        ConvertDateTimeCommand = new RelayCommand(ConvertDateTime);
        ConvertTimeZoneCommand = new RelayCommand(ConvertTimeZone);
        CalculateDifferenceCommand = new RelayCommand(CalculateDifference);
        RefreshCurrentTime();
    }

    public IReadOnlyList<TimeZoneInfo> SourceTimeZones { get => _sourceTimeZones; private set => SetProperty(ref _sourceTimeZones, value); }
    public IReadOnlyList<TimeZoneInfo> TargetTimeZones { get => _targetTimeZones; private set => SetProperty(ref _targetTimeZones, value); }
    public RelayCommand<string> CopyTextCommand { get; }
    public RelayCommand ConvertTimestampCommand { get; }
    public RelayCommand ConvertDateTimeCommand { get; }
    public RelayCommand ConvertTimeZoneCommand { get; }
    public RelayCommand CalculateDifferenceCommand { get; }
    public string CopyStatus { get => _copyStatus; private set => SetProperty(ref _copyStatus, value); }

    public string CurrentLocalTime => _currentUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
    public string CurrentUtcTime => _currentUtc.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
    public string CurrentUnixSeconds => TimeConversionService.ToUnixTimestamp(_currentUtc, UnixTimestampUnit.Seconds).ToString(CultureInfo.InvariantCulture);
    public string CurrentUnixMilliseconds => TimeConversionService.ToUnixTimestamp(_currentUtc, UnixTimestampUnit.Milliseconds).ToString(CultureInfo.InvariantCulture);
    public string CurrentIso => TimeConversionService.FormatIso(_currentUtc.ToLocalTime());
    public bool IsActive => _active;
    public string TimestampText { get => _timestampText; set => SetProperty(ref _timestampText, value ?? string.Empty); }
    public int TimestampUnitIndex
    {
        get => _timestampUnitIndex;
        set
        {
            if (value == -1) return;
            if (value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
            SetProperty(ref _timestampUnitIndex, value);
        }
    }
    public string TimestampStatus { get => _timestampStatus; private set => SetProperty(ref _timestampStatus, value); }
    public string ConvertedUtc { get => _convertedUtc; private set => SetProperty(ref _convertedUtc, value); }
    public string ConvertedLocal { get => _convertedLocal; private set => SetProperty(ref _convertedLocal, value); }
    public string ConvertedIso { get => _convertedIso; private set => SetProperty(ref _convertedIso, value); }
    public string UnixSeconds { get => _unixSeconds; private set => SetProperty(ref _unixSeconds, value); }
    public string UnixMilliseconds { get => _unixMilliseconds; private set => SetProperty(ref _unixMilliseconds, value); }
    public string DateTimeInput { get => _dateTimeInput; set => SetProperty(ref _dateTimeInput, value ?? string.Empty); }
    public string DateTimeStatus { get => _dateTimeStatus; private set => SetProperty(ref _dateTimeStatus, value); }
    public string SourceZoneSearch { get => _sourceZoneSearch; set { if (SetProperty(ref _sourceZoneSearch, value ?? string.Empty)) RefreshSourceTimeZones(); } }
    public string TargetZoneSearch { get => _targetZoneSearch; set { if (SetProperty(ref _targetZoneSearch, value ?? string.Empty)) RefreshTargetTimeZones(); } }
    public TimeZoneInfo SourceTimeZone
    {
        get => _sourceTimeZone;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _sourceTimeZone, value))
            {
                RefreshSourceTimeZones();
                OnPropertyChanged(nameof(SourceTimeZoneLabel));
            }
        }
    }
    public TimeZoneInfo TargetTimeZone
    {
        get => _targetTimeZone;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (SetProperty(ref _targetTimeZone, value))
            {
                RefreshTargetTimeZones();
                OnPropertyChanged(nameof(TargetTimeZoneLabel));
            }
        }
    }
    public string SourceTimeZoneLabel => $"{SourceTimeZone.DisplayName} · {SourceTimeZone.Id}";
    public string TargetTimeZoneLabel => $"{TargetTimeZone.DisplayName} · {TargetTimeZone.Id}";
    public string TimeZoneInput { get => _timeZoneInput; set => SetProperty(ref _timeZoneInput, value ?? string.Empty); }
    public string TimeZoneStatus { get => _timeZoneStatus; private set => SetProperty(ref _timeZoneStatus, value); }
    public string TimeZoneTarget { get => _timeZoneTarget; private set => SetProperty(ref _timeZoneTarget, value); }
    public string TimeZoneUtc { get => _timeZoneUtc; private set => SetProperty(ref _timeZoneUtc, value); }
    public string TimeZoneIso { get => _timeZoneIso; private set => SetProperty(ref _timeZoneIso, value); }
    public IReadOnlyList<TimeSpan> DateTimeAmbiguousOffsets { get => _dateTimeAmbiguousOffsets; private set { if (SetProperty(ref _dateTimeAmbiguousOffsets, value)) OnPropertyChanged(nameof(HasDateTimeAmbiguousOffsets)); } }
    public bool HasDateTimeAmbiguousOffsets => DateTimeAmbiguousOffsets.Count > 0;
    public TimeSpan? SelectedDateTimeAmbiguousOffset
    {
        get => _selectedDateTimeAmbiguousOffset;
        set { if (SetProperty(ref _selectedDateTimeAmbiguousOffset, value)) ConvertDateTime(); }
    }
    public IReadOnlyList<TimeSpan> TimeZoneAmbiguousOffsets { get => _timeZoneAmbiguousOffsets; private set { if (SetProperty(ref _timeZoneAmbiguousOffsets, value)) OnPropertyChanged(nameof(HasTimeZoneAmbiguousOffsets)); } }
    public bool HasTimeZoneAmbiguousOffsets => TimeZoneAmbiguousOffsets.Count > 0;
    public TimeSpan? SelectedTimeZoneAmbiguousOffset
    {
        get => _selectedTimeZoneAmbiguousOffset;
        set { if (SetProperty(ref _selectedTimeZoneAmbiguousOffset, value)) ConvertTimeZone(); }
    }
    public string StartInput { get => _startInput; set => SetProperty(ref _startInput, value ?? string.Empty); }
    public string EndInput { get => _endInput; set => SetProperty(ref _endInput, value ?? string.Empty); }
    public string DurationStatus { get => _durationStatus; private set => SetProperty(ref _durationStatus, value); }
    public string DurationDisplay { get => _durationDisplay; private set => SetProperty(ref _durationDisplay, value); }
    public string DurationDays { get => _durationDays; private set => SetProperty(ref _durationDays, value); }
    public string DurationHours { get => _durationHours; private set => SetProperty(ref _durationHours, value); }
    public string DurationMinutes { get => _durationMinutes; private set => SetProperty(ref _durationMinutes, value); }
    public string DurationSeconds { get => _durationSeconds; private set => SetProperty(ref _durationSeconds, value); }
    public string DurationTotalSeconds { get => _durationTotalSeconds; private set => SetProperty(ref _durationTotalSeconds, value); }
    public string DurationTotalMilliseconds { get => _durationTotalMilliseconds; private set => SetProperty(ref _durationTotalMilliseconds, value); }

    public void Activate()
    {
        if (_disposed || _active) return;
        _active = true;
        OnPropertyChanged(nameof(IsActive));
        RefreshCurrentTime();
        _timer.Start();
    }

    public void Deactivate()
    {
        if (!_active) return;
        _active = false;
        _timer.Stop();
        OnPropertyChanged(nameof(IsActive));
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Deactivate();
        _timer.Dispose();
    }

    private void RefreshCurrentTime()
    {
        if (_disposed) return;
        _currentUtc = _clock.UtcNow.ToUniversalTime();
        OnPropertyChanged(nameof(CurrentLocalTime));
        OnPropertyChanged(nameof(CurrentUtcTime));
        OnPropertyChanged(nameof(CurrentUnixSeconds));
        OnPropertyChanged(nameof(CurrentUnixMilliseconds));
        OnPropertyChanged(nameof(CurrentIso));
    }

    private void ConvertTimestamp()
    {
        ConvertedUtc = ConvertedLocal = ConvertedIso = string.Empty;
        TimestampStatus = string.Empty;
        if (!long.TryParse(TimestampText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
        {
            TimestampStatus = "请输入有效的整数时间戳。";
            return;
        }
        try
        {
            DateTimeOffset instant = TimeConversionService.FromUnixTimestamp(value,
                TimestampUnitIndex == 0 ? UnixTimestampUnit.Seconds : UnixTimestampUnit.Milliseconds);
            ConvertedUtc = instant.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            ConvertedLocal = instant.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
            ConvertedIso = TimeConversionService.FormatIso(instant);
        }
        catch (ArgumentOutOfRangeException)
        {
            TimestampStatus = "时间戳超出支持范围。";
        }
    }

    private void ConvertDateTime()
    {
        UnixSeconds = UnixMilliseconds = string.Empty;
        bool success = TimeConversionService.TryParseIso(DateTimeInput, SourceTimeZone, SelectedDateTimeAmbiguousOffset,
            out DateTimeOffset instant, out TimeZoneResolutionStatus status, out IReadOnlyList<TimeSpan> offsets);
        DateTimeAmbiguousOffsets = offsets;
        if (!success)
        {
            DateTimeStatus = StatusMessage(status);
            return;
        }
        DateTimeStatus = string.Empty;
        UnixSeconds = TimeConversionService.ToUnixTimestamp(instant, UnixTimestampUnit.Seconds).ToString(CultureInfo.InvariantCulture);
        UnixMilliseconds = TimeConversionService.ToUnixTimestamp(instant, UnixTimestampUnit.Milliseconds).ToString(CultureInfo.InvariantCulture);
    }

    private void ConvertTimeZone()
    {
        TimeZoneTarget = TimeZoneUtc = TimeZoneIso = string.Empty;
        bool success = TimeConversionService.TryParseIso(TimeZoneInput, SourceTimeZone, SelectedTimeZoneAmbiguousOffset,
            out DateTimeOffset instant, out TimeZoneResolutionStatus status, out IReadOnlyList<TimeSpan> offsets);
        TimeZoneAmbiguousOffsets = offsets;
        if (!success)
        {
            TimeZoneStatus = StatusMessage(status);
            return;
        }
        DateTimeOffset target;
        try { target = TimeZoneInfo.ConvertTime(instant, TargetTimeZone); }
        catch (ArgumentException)
        {
            TimeZoneStatus = "该日期时间无法在目标时区表示。";
            return;
        }
        TimeZoneStatus = string.Empty;
        TimeZoneTarget = target.ToString("yyyy-MM-dd HH:mm:ss zzz", CultureInfo.InvariantCulture);
        TimeZoneUtc = target.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        TimeZoneIso = TimeConversionService.FormatIso(target);
    }

    private void CalculateDifference()
    {
        DurationDisplay = DurationDays = DurationHours = DurationMinutes = DurationSeconds =
            DurationTotalSeconds = DurationTotalMilliseconds = string.Empty;
        bool startOk = TimeConversionService.TryParseIso(StartInput, SourceTimeZone, null,
            out DateTimeOffset start, out TimeZoneResolutionStatus startStatus, out _);
        bool endOk = TimeConversionService.TryParseIso(EndInput, SourceTimeZone, null,
            out DateTimeOffset end, out TimeZoneResolutionStatus endStatus, out _);
        if (!startOk || !endOk)
        {
            DurationStatus = StatusMessage(!startOk ? startStatus : endStatus);
            return;
        }

        TimeSpan difference = TimeConversionService.Difference(start, end);
        DurationStatus = string.Empty;
        DurationDisplay = difference.ToString("c", CultureInfo.InvariantCulture);
        DurationDays = difference.Days.ToString(CultureInfo.InvariantCulture);
        DurationHours = difference.Hours.ToString(CultureInfo.InvariantCulture);
        DurationMinutes = difference.Minutes.ToString(CultureInfo.InvariantCulture);
        DurationSeconds = difference.Seconds.ToString(CultureInfo.InvariantCulture);
        DurationTotalSeconds = difference.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);
        DurationTotalMilliseconds = difference.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture);
    }

    private void CopyText(string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        try { _clipboard.SetText(text); CopyStatus = "已复制。"; }
        catch { CopyStatus = "无法写入剪贴板。"; }
    }

    private static string StatusMessage(TimeZoneResolutionStatus status) => status switch
    {
        TimeZoneResolutionStatus.InvalidLocalTime => "该本地时间因夏令时切换而不存在。",
        TimeZoneResolutionStatus.AmbiguousLocalTime => "该时间在当前时区存在两个可能的 UTC Offset，请选择一个。",
        TimeZoneResolutionStatus.InvalidAmbiguousOffset => "所选 UTC Offset 不适用于该本地时间。",
        _ => "无法识别该日期时间格式。"
    };

    private void RefreshSourceTimeZones() => SourceTimeZones = FilterZones(SourceZoneSearch, SourceTimeZone);
    private void RefreshTargetTimeZones() => TargetTimeZones = FilterZones(TargetZoneSearch, TargetTimeZone);

    private static IReadOnlyList<TimeZoneInfo> FilterZones(string query, TimeZoneInfo selected)
    {
        IReadOnlyList<TimeZoneInfo> matches = TimeZoneCatalog.Search(query);
        return matches.Contains(selected) ? matches : new[] { selected }.Concat(matches).ToArray();
    }
}
