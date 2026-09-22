using System.Collections.ObjectModel;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.ClipboardPlus.Models;
using WindowsToolbox.Modules.ClipboardPlus.Services;

namespace WindowsToolbox.Modules.ClipboardPlus.ViewModels;

public sealed class ClipboardPlusViewModel : ObservableObject
{
    private readonly IClipboardPlusService _service;
    private readonly ISettingsService _settingsService;
    private string _searchText = string.Empty;
    private ClipboardHistoryItem? _selectedItem;
    private string _notificationMessage = string.Empty;
    private bool _isEnabled;
    private bool _isPaused;

    public ClipboardPlusViewModel(IClipboardPlusService service, ISettingsService settingsService)
    {
        _service = service;
        _settingsService = settingsService;
        _isEnabled = settingsService.Settings.ClipboardPlusEnabled;
        _isPaused = settingsService.Settings.ClipboardPlusPaused;
        CapacityOptions = ClipboardOptions.Capacities;
        RetentionOptions = ClipboardOptions.RetentionDays;
        _service.Changed += (_, _) => RefreshItems();
        _service.Notice += (_, message) => NotificationMessage = message;
        _service.HotkeyPressed += (_, _) => OnPropertyChanged(nameof(HotkeyActivated));

        EnableCommand = new RelayCommand(ToggleEnabled);
        PauseCommand = new RelayCommand(TogglePaused);
        CopyCommand = new RelayCommand<ClipboardHistoryItem>(item => { if (item is not null) ExecuteCopy(item); });
        DeleteCommand = new RelayCommand<ClipboardHistoryItem>(item => { if (item is not null) ExecuteDelete(item); });
        PinCommand = new RelayCommand<ClipboardHistoryItem>(item => { if (item is not null) ExecutePin(item); });
        ClearCommand = new RelayCommand(Clear);
        _ = InitializeAsync();
    }

    public ObservableCollection<ClipboardHistoryItem> VisibleItems { get; } = [];
    public IReadOnlyList<int> CapacityOptions { get; }
    public IReadOnlyList<int> RetentionOptions { get; }
    public bool IsListening => _service.IsListening;
    public bool IsHotkeyRegistered => _service.IsHotkeyRegistered;
    public bool IsActiveInBackground => IsEnabled && !IsPaused && IsListening;
    public bool HotkeyActivated { get; private set; }
    public bool IsEnabled
    {
        get => _isEnabled;
        private set { if (SetProperty(ref _isEnabled, value)) OnPropertyChanged(nameof(StatusText)); }
    }
    public bool IsPaused
    {
        get => _isPaused;
        private set { if (SetProperty(ref _isPaused, value)) OnPropertyChanged(nameof(StatusText)); }
    }
    public string StatusText => !IsEnabled ? "已关闭" : IsPaused ? "已暂停" : IsListening ? "监听中" : "启动失败";
    public string SearchText
    {
        get => _searchText;
        set { if (SetProperty(ref _searchText, value)) RefreshItems(); }
    }
    public ClipboardHistoryItem? SelectedItem
    {
        get => _selectedItem;
        set => SetProperty(ref _selectedItem, value);
    }
    public string NotificationMessage
    {
        get => _notificationMessage;
        private set { if (SetProperty(ref _notificationMessage, value)) OnPropertyChanged(nameof(HasNotification)); }
    }
    public bool HasNotification => !string.IsNullOrWhiteSpace(NotificationMessage);
    public string RetentionText => _settingsService.Settings.ClipboardPlusRetentionDays == 0
        ? "永久" : $"{_settingsService.Settings.ClipboardPlusRetentionDays} 天";
    public int Capacity
    {
        get => _settingsService.Settings.ClipboardPlusCapacity;
        set
        {
            if (Capacity != value) { _service.SetCapacity(value); OnPropertyChanged(); OnPropertyChanged(nameof(Capacity)); SaveSettings(); }
        }
    }
    public int RetentionDays
    {
        get => _settingsService.Settings.ClipboardPlusRetentionDays;
        set
        {
            if (RetentionDays != value) { _service.SetRetentionDays(value); OnPropertyChanged(); OnPropertyChanged(nameof(RetentionText)); SaveSettings(); }
        }
    }

    public RelayCommand EnableCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand<ClipboardHistoryItem> CopyCommand { get; }
    public RelayCommand<ClipboardHistoryItem> DeleteCommand { get; }
    public RelayCommand<ClipboardHistoryItem> PinCommand { get; }
    public RelayCommand ClearCommand { get; }

    private async Task InitializeAsync()
    {
        await _service.LoadAsync();
        if (IsEnabled && !IsPaused)
            _service.Start();
        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(IsHotkeyRegistered));
        OnPropertyChanged(nameof(StatusText));
    }

    private void ToggleEnabled()
    {
        IsEnabled = !IsEnabled;
        _settingsService.Settings.ClipboardPlusEnabled = IsEnabled;
        if (IsEnabled && !IsPaused) _service.Start(); else _service.Stop();
        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(IsHotkeyRegistered));
        SaveSettings();
    }

    private void TogglePaused()
    {
        IsPaused = !IsPaused;
        _settingsService.Settings.ClipboardPlusPaused = IsPaused;
        if (IsPaused) _service.Stop(); else if (IsEnabled) _service.Start();
        OnPropertyChanged(nameof(IsListening));
        OnPropertyChanged(nameof(IsHotkeyRegistered));
        SaveSettings();
    }

    private bool ExecuteCopy(ClipboardHistoryItem? item)
    {
        if (item is null) return false;
        _service.Copy(item);
        return true;
    }

    private bool ExecuteDelete(ClipboardHistoryItem? item)
    {
        if (item is null) return false;
        _service.Delete(item.Id);
        if (SelectedItem?.Id == item.Id) SelectedItem = null;
        return true;
    }

    private bool ExecutePin(ClipboardHistoryItem? item)
    {
        if (item is null) return false;
        _service.SetPinned(item.Id, !item.IsPinned);
        return true;
    }

    private void Clear() => _service.Clear();

    private void RefreshItems()
    {
        string query = SearchText.Trim();
        VisibleItems.Clear();
        foreach (ClipboardHistoryItem item in _service.Items)
        {
            if (query.Length == 0 ||
                item.Text.Contains(query, StringComparison.CurrentCultureIgnoreCase) ||
                item.SourceDisplayName.Contains(query, StringComparison.CurrentCultureIgnoreCase))
                VisibleItems.Add(item);
        }
        OnPropertyChanged(nameof(StatusText));
    }

    private void SaveSettings() => _ = _settingsService.SaveAsync();

    public void StopForExit() => _service.Dispose();
}
