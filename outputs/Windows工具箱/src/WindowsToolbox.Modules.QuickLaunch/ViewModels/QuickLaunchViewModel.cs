using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using WindowsToolbox.Core.Commands;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.QuickLaunch.Models;
using WindowsToolbox.Modules.QuickLaunch.Services;

namespace WindowsToolbox.Modules.QuickLaunch.ViewModels;

public sealed class QuickLaunchViewModel : ObservableObject, IDisposable
{
    private readonly IQuickLaunchStore _store;
    private readonly IQuickLaunchExecutor _executor;
    private readonly IQuickLaunchIconService _icons;
    private readonly List<QuickLaunchItem> _items = [];
    private readonly List<QuickLaunchGroup> _groups = [];
    private CancellationTokenSource? _searchCancellation;
    private string _searchText = string.Empty;
    private string _status = "固定常用应用、文件夹、文件和网页，双击或单击即可打开。";
    private string _error = string.Empty;
    private bool _isEditing;
    private string? _editingId;
    private QuickLaunchItemType _editingType;
    private string _editingName = string.Empty;
    private string _editingTarget = string.Empty;
    private string _editingGroup = string.Empty;
    private string _editingArguments = string.Empty;
    private string _editingWorkingDirectory = string.Empty;
    private int _selectedIndex = -1;

    public QuickLaunchViewModel(IQuickLaunchStore store, IQuickLaunchExecutor executor, IQuickLaunchIconService icons)
    {
        _store = store;
        _executor = executor;
        _icons = icons;
        LaunchCommand = new RelayCommand<QuickLaunchItemViewModel>(item => _ = LaunchAsync(item));
        OpenLocationCommand = new RelayCommand<QuickLaunchItemViewModel>(item => _ = OpenLocationAsync(item));
        TogglePinCommand = new RelayCommand<QuickLaunchItemViewModel>(TogglePin);
        RemoveCommand = new RelayCommand<QuickLaunchItemViewModel>(Remove);
        MoveUpCommand = new RelayCommand<QuickLaunchItemViewModel>(item => Move(item, -1));
        MoveDownCommand = new RelayCommand<QuickLaunchItemViewModel>(item => Move(item, 1));
        SaveEditCommand = new AsyncRelayCommand(SaveEditAsync, () => IsEditing && CanSaveEdit);
        CancelEditCommand = new RelayCommand(CancelEdit);
        _ = LoadAsync();
    }

    public ObservableCollection<QuickLaunchItemViewModel> Items { get; } = [];
    public ObservableCollection<QuickLaunchItemViewModel> PinnedItems { get; } = [];
    public ObservableCollection<QuickLaunchItemViewModel> RecentItems { get; } = [];
    public ObservableCollection<string> GroupNames { get; } = [];
    public IReadOnlyList<QuickLaunchItemType> ItemTypes { get; } = Enum.GetValues<QuickLaunchItemType>();
    public RelayCommand<QuickLaunchItemViewModel> LaunchCommand { get; }
    public RelayCommand<QuickLaunchItemViewModel> OpenLocationCommand { get; }
    public RelayCommand<QuickLaunchItemViewModel> TogglePinCommand { get; }
    public RelayCommand<QuickLaunchItemViewModel> RemoveCommand { get; }
    public RelayCommand<QuickLaunchItemViewModel> MoveUpCommand { get; }
    public RelayCommand<QuickLaunchItemViewModel> MoveDownCommand { get; }
    public AsyncRelayCommand SaveEditCommand { get; }
    public RelayCommand CancelEditCommand { get; }
    public event EventHandler? FocusSearchRequested;

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (!SetProperty(ref _searchText, value ?? string.Empty)) return;
            _searchCancellation?.Cancel();
            CancellationTokenSource source = _searchCancellation = new();
            _ = DebouncedRefreshAsync(source.Token);
        }
    }

    public string Status { get => _status; private set => SetProperty(ref _status, value); }
    public string Error { get => _error; private set => SetProperty(ref _error, value); }
    public bool IsEditing { get => _isEditing; private set => SetProperty(ref _isEditing, value); }
    public QuickLaunchItemType EditingType { get => _editingType; set { if (SetProperty(ref _editingType, value)) SaveEditCommand.NotifyCanExecuteChanged(); } }
    public string EditingTypeText => QuickLaunchItemRules.TypeText(EditingType);
    public string EditingName { get => _editingName; set { if (SetProperty(ref _editingName, value ?? string.Empty)) SaveEditCommand.NotifyCanExecuteChanged(); } }
    public string EditingTarget { get => _editingTarget; set { if (SetProperty(ref _editingTarget, value ?? string.Empty)) SaveEditCommand.NotifyCanExecuteChanged(); } }
    public string EditingGroup { get => _editingGroup; set => SetProperty(ref _editingGroup, value ?? string.Empty); }
    public string EditingArguments { get => _editingArguments; set => SetProperty(ref _editingArguments, value ?? string.Empty); }
    public string EditingWorkingDirectory { get => _editingWorkingDirectory; set => SetProperty(ref _editingWorkingDirectory, value ?? string.Empty); }
    public bool CanSaveEdit => !string.IsNullOrWhiteSpace(EditingName) && QuickLaunchItemRules.IsValidTarget(EditingType, EditingTarget);
    public int ItemCount => _items.Count;
    public QuickLaunchItemViewModel? SelectedItem => _selectedIndex >= 0 && _selectedIndex < Items.Count ? Items[_selectedIndex] : null;

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        QuickLaunchLoadResult result = await _store.LoadAsync(cancellationToken).ConfigureAwait(true);
        _items.Clear();
        _items.AddRange(result.Items.Where(IsSafeItem));
        _groups.Clear();
        _groups.AddRange(result.Groups.Where(group => !string.IsNullOrWhiteSpace(group.Name)));
        NormalizeOrders();
        RefreshCollections();
        if (result.WasCorrupt)
            Status = "快捷启动数据无法读取，已创建空列表。";
    }

    public void BeginAdd(QuickLaunchItemType type, string target = "")
    {
        _editingId = null;
        EditingType = type;
        EditingTarget = type == QuickLaunchItemType.Url ? target : QuickLaunchItemRules.NormalizePath(target);
        EditingName = GuessName(type, target);
        EditingGroup = string.Empty;
        EditingArguments = string.Empty;
        EditingWorkingDirectory = type == QuickLaunchItemType.Application ? Path.GetDirectoryName(target) ?? string.Empty : string.Empty;
        IsEditing = true;
        Error = string.Empty;
        SaveEditCommand.NotifyCanExecuteChanged();
    }

    public void BeginEdit(QuickLaunchItemViewModel? item)
    {
        if (item is null) return;
        QuickLaunchItem model = item.Model;
        _editingId = model.Id;
        EditingType = model.Type;
        EditingName = model.Name;
        EditingTarget = model.Target;
        EditingGroup = model.Group;
        EditingArguments = model.Arguments;
        EditingWorkingDirectory = model.WorkingDirectory;
        IsEditing = true;
        Error = string.Empty;
        SaveEditCommand.NotifyCanExecuteChanged();
    }

    public void AddDroppedPaths(IEnumerable<string> paths)
    {
        int added = 0;
        foreach (string path in paths.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            QuickLaunchItemType type = Directory.Exists(path)
                ? QuickLaunchItemType.Folder
                : Path.GetExtension(path).Equals(".exe", StringComparison.OrdinalIgnoreCase)
                    ? QuickLaunchItemType.Application
                    : QuickLaunchItemType.File;
            if (AddItem(CreateItem(type, path))) added++;
        }
        if (added > 0) _ = SaveAsync();
        Status = added == 0 ? "没有添加新的快捷项。" : $"已添加 {added} 个快捷项。";
    }

    public void RequestFocusSearch() => FocusSearchRequested?.Invoke(this, EventArgs.Empty);

    public void SetHotkeyStatus(bool registered)
    {
        if (!registered && Status.StartsWith("固定常用", StringComparison.Ordinal))
            Status = "快捷键不可用：Win+Alt+Q 可能已被其他程序占用。仍可在本页使用快捷启动。";
    }

    public void MoveSelection(int offset)
    {
        if (Items.Count == 0) { _selectedIndex = -1; OnPropertyChanged(nameof(SelectedItem)); return; }
        _selectedIndex = Math.Clamp(_selectedIndex < 0 ? 0 : _selectedIndex + offset, 0, Items.Count - 1);
        OnPropertyChanged(nameof(SelectedItem));
    }

    public void LaunchSelected() => LaunchCommand.Execute(SelectedItem ?? Items.FirstOrDefault());

    public void LaunchItem(QuickLaunchItemViewModel? item) => LaunchCommand.Execute(item);
    public void OpenLocationItem(QuickLaunchItemViewModel? item) => OpenLocationCommand.Execute(item);
    public void TogglePinItem(QuickLaunchItemViewModel? item) => TogglePinCommand.Execute(item);
    public void RemoveItem(QuickLaunchItemViewModel? item) => RemoveCommand.Execute(item);

    public void CreateGroup(string name)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name) || _groups.Any(group => string.Equals(group.Name, name, StringComparison.OrdinalIgnoreCase))) return;
        _groups.Add(new QuickLaunchGroup { Name = name, Order = _groups.Count });
        RefreshCollections();
        _ = SaveAsync();
    }

    public void RenameGroup(string oldName, string newName)
    {
        newName = newName.Trim();
        if (string.IsNullOrWhiteSpace(newName) || _groups.Any(group => string.Equals(group.Name, newName, StringComparison.OrdinalIgnoreCase))) return;
        QuickLaunchGroup? group = _groups.FirstOrDefault(item => string.Equals(item.Name, oldName, StringComparison.OrdinalIgnoreCase));
        if (group is null) return;
        group.Name = newName;
        foreach (QuickLaunchItem item in _items.Where(item => string.Equals(item.Group, oldName, StringComparison.OrdinalIgnoreCase))) item.Group = newName;
        RefreshCollections();
        _ = SaveAsync();
    }

    public async Task SaveEditAsync()
    {
        if (!CanSaveEdit) { Error = "请填写有效名称和目标。"; return; }
        QuickLaunchItem? item = _editingId is null ? null : _items.FirstOrDefault(value => value.Id == _editingId);
        if (item is null)
        {
            item = CreateItem(EditingType, EditingTarget);
            _items.Add(item);
        }
        item.Name = EditingName.Trim();
        item.Target = EditingType == QuickLaunchItemType.Url ? EditingTarget.Trim() : QuickLaunchItemRules.NormalizePath(EditingTarget);
        item.Type = EditingType;
        item.Group = EditingGroup.Trim();
        item.Arguments = EditingArguments.Trim();
        item.WorkingDirectory = EditingWorkingDirectory.Trim();
        if (_items.Any(existing => existing.Id != item.Id &&
            string.Equals(QuickLaunchItemRules.NormalizeKey(existing), QuickLaunchItemRules.NormalizeKey(item), StringComparison.OrdinalIgnoreCase)))
        {
            Error = "该目标已经存在于快捷启动中。";
            return;
        }
        if (!string.IsNullOrWhiteSpace(item.Group) && !_groups.Any(group => string.Equals(group.Name, item.Group, StringComparison.OrdinalIgnoreCase)))
            _groups.Add(new QuickLaunchGroup { Name = item.Group, Order = _groups.Count });
        NormalizeOrders();
        await SaveAsync().ConfigureAwait(true);
        IsEditing = false;
        Status = "快捷项已保存。";
        RefreshCollections();
    }

    private async Task LaunchAsync(QuickLaunchItemViewModel? viewModel)
    {
        if (viewModel is null) return;
        QuickLaunchLaunchResult result = await _executor.LaunchAsync(viewModel.Model).ConfigureAwait(true);
        if (!result.Success) { Error = result.Message; Status = result.Message; return; }
        QuickLaunchItem item = viewModel.Model;
        item.LastUsedAt = DateTimeOffset.Now;
        item.LaunchCount++;
        Error = string.Empty;
        Status = $"已打开：{item.Name}";
        RefreshCollections();
        await SaveAsync().ConfigureAwait(true);
    }

    private async Task OpenLocationAsync(QuickLaunchItemViewModel? viewModel)
    {
        if (viewModel is null) return;
        QuickLaunchLaunchResult result = await _executor.OpenLocationAsync(viewModel.Model).ConfigureAwait(true);
        Error = result.Success ? string.Empty : result.Message;
        Status = result.Message;
    }

    private void TogglePin(QuickLaunchItemViewModel? viewModel)
    {
        if (viewModel is null) return;
        viewModel.Model.IsPinned = !viewModel.Model.IsPinned;
        RefreshCollections();
        _ = SaveAsync();
    }

    private void Remove(QuickLaunchItemViewModel? viewModel)
    {
        if (viewModel is null) return;
        _items.RemoveAll(item => item.Id == viewModel.Id);
        RefreshCollections();
        Status = "已从快捷启动中移除。";
        _ = SaveAsync();
    }

    private void Move(QuickLaunchItemViewModel? viewModel, int offset)
    {
        if (viewModel is null) return;
        QuickLaunchItem[] ordered = _items.Where(item => string.Equals(item.Group, viewModel.Model.Group, StringComparison.OrdinalIgnoreCase)).OrderBy(item => item.Order).ToArray();
        int index = Array.FindIndex(ordered, item => item.Id == viewModel.Id);
        int next = index + offset;
        if (index < 0 || next < 0 || next >= ordered.Length) return;
        (ordered[index].Order, ordered[next].Order) = (ordered[next].Order, ordered[index].Order);
        RefreshCollections();
        _ = SaveAsync();
    }

    private async Task DebouncedRefreshAsync(CancellationToken token)
    {
        try { await Task.Delay(120, token).ConfigureAwait(true); RefreshCollections(); }
        catch (OperationCanceledException) { }
    }

    private void RefreshCollections()
    {
        IEnumerable<QuickLaunchItem> visible = FilterItems();
        Items.Clear();
        foreach (QuickLaunchItem item in visible) Items.Add(CreateViewModel(item));
        _selectedIndex = Items.Count == 0 ? -1 : Math.Clamp(_selectedIndex, 0, Items.Count - 1);
        OnPropertyChanged(nameof(SelectedItem));
        PinnedItems.Clear();
        foreach (QuickLaunchItem item in visible.Where(item => item.IsPinned)) PinnedItems.Add(CreateViewModel(item));
        RecentItems.Clear();
        foreach (QuickLaunchItem item in visible.Where(item => item.LastUsedAt is not null).OrderByDescending(item => item.LastUsedAt).Take(8)) RecentItems.Add(CreateViewModel(item));
        GroupNames.Clear();
        foreach (QuickLaunchGroup group in _groups.OrderBy(group => group.Order)) GroupNames.Add(group.Name);
        OnPropertyChanged(nameof(ItemCount));
    }

    private IEnumerable<QuickLaunchItem> FilterItems()
    {
        string query = SearchText.Trim();
        IEnumerable<QuickLaunchItem> items = _items;
        if (!string.IsNullOrWhiteSpace(query))
            items = items.Where(item => item.Name.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Target.Contains(query, StringComparison.CurrentCultureIgnoreCase) || item.Group.Contains(query, StringComparison.CurrentCultureIgnoreCase));
        return items.OrderByDescending(item => item.IsPinned).ThenBy(item => GroupOrder(item.Group)).ThenBy(item => item.Order).ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
    }

    private QuickLaunchItemViewModel CreateViewModel(QuickLaunchItem item)
    {
        QuickLaunchItemViewModel viewModel = new(item);
        _ = LoadIconAsync(viewModel);
        return viewModel;
    }

    private async Task LoadIconAsync(QuickLaunchItemViewModel viewModel)
    {
        try
        {
            viewModel.Icon = await _icons.GetAsync(viewModel.Model).ConfigureAwait(true);
            viewModel.Refresh();
        }
        catch { }
    }

    private bool AddItem(QuickLaunchItem item)
    {
        if (!IsSafeItem(item) || _items.Any(existing => string.Equals(QuickLaunchItemRules.NormalizeKey(existing), QuickLaunchItemRules.NormalizeKey(item), StringComparison.OrdinalIgnoreCase))) return false;
        item.Order = _items.Count(existing => string.Equals(existing.Group, item.Group, StringComparison.OrdinalIgnoreCase));
        _items.Add(item);
        RefreshCollections();
        return true;
    }

    private QuickLaunchItem CreateItem(QuickLaunchItemType type, string target) => new()
    {
        Name = GuessName(type, target),
        Target = type == QuickLaunchItemType.Url ? target.Trim() : QuickLaunchItemRules.NormalizePath(target),
        Type = type,
        CreatedAt = DateTimeOffset.Now
    };

    private static string GuessName(QuickLaunchItemType type, string target)
    {
        if (type == QuickLaunchItemType.Url) return target.Trim();
        string name = type == QuickLaunchItemType.Folder ? new DirectoryInfo(target).Name : Path.GetFileNameWithoutExtension(target);
        if (type == QuickLaunchItemType.Application && File.Exists(target))
        {
            try
            {
                FileVersionInfo info = FileVersionInfo.GetVersionInfo(target);
                name = info.ProductName ?? info.FileDescription ?? name;
            }
            catch { }
        }
        return string.IsNullOrWhiteSpace(name) ? target : name;
    }

    private bool IsSafeItem(QuickLaunchItem item) =>
        !string.IsNullOrWhiteSpace(item.Id) && !string.IsNullOrWhiteSpace(item.Name) && QuickLaunchItemRules.IsValidTarget(item.Type, item.Target);

    private int GroupOrder(string group) => _groups.FirstOrDefault(item => string.Equals(item.Name, group, StringComparison.OrdinalIgnoreCase))?.Order ?? int.MaxValue;

    private void NormalizeOrders()
    {
        foreach ((QuickLaunchItem item, int index) in _items.GroupBy(item => item.Group, StringComparer.OrdinalIgnoreCase).SelectMany(group => group.OrderBy(item => item.Order).Select((item, index) => (item, index))))
            item.Order = index;
    }

    private async Task SaveAsync()
    {
        try { await _store.SaveAsync(_items, _groups).ConfigureAwait(true); }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { Error = "快捷启动数据保存失败。"; }
    }

    private void CancelEdit()
    {
        IsEditing = false;
        Error = string.Empty;
    }

    public void Dispose()
    {
        _searchCancellation?.Cancel();
        _searchCancellation?.Dispose();
    }
}
