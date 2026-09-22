using System.Text;
using System.Runtime.InteropServices;
using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Modules.ClipboardPlus.Models;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed class ClipboardPlusService : IClipboardPlusService
{
    private readonly IClipboardAdapter _adapter;
    private readonly IClipboardHistoryStore _store;
    private readonly IClipboardSourceResolver _sourceResolver;
    private readonly IClipboardListener _listener;
    private readonly ISettingsService _settings;
    private readonly List<ClipboardHistoryItem> _items = [];
    private CancellationTokenSource? _saveCancellation;
    private uint _selfSequence;

    public ClipboardPlusService(
        IClipboardAdapter adapter,
        IClipboardHistoryStore store,
        IClipboardSourceResolver sourceResolver,
        IClipboardListener listener,
        ISettingsService settings)
    {
        _adapter = adapter;
        _store = store;
        _sourceResolver = sourceResolver;
        _listener = listener;
        _settings = settings;
        _listener.ClipboardChanged += Listener_ClipboardChanged;
        _listener.HotkeyPressed += (_, _) => HotkeyPressed?.Invoke(this, EventArgs.Empty);
    }

    public event EventHandler? Changed;
    public event EventHandler<string>? Notice;
    public event EventHandler? HotkeyPressed;
    public IReadOnlyList<ClipboardHistoryItem> Items => _items;
    public bool IsListening => _listener.IsRunning;
    public bool IsHotkeyRegistered => _listener.IsHotkeyRegistered;

    public async Task LoadAsync()
    {
        _items.Clear();
        _items.AddRange(await _store.LoadAsync());
        Prune();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool Start()
    {
        if (_listener.Start(_settings.Settings.ClipboardPlusHotkeyEnabled))
        {
            if (!_listener.IsHotkeyRegistered)
                Notice?.Invoke(this, "剪贴板监听已启动，但 Win+Alt+V 全局快捷键注册失败，可在设置中继续使用。");
            return true;
        }

        Notice?.Invoke(this, "剪贴板监听启动失败，请检查系统剪贴板权限后重试。");
        return false;
    }

    public void Stop() => _listener.Stop();

    public void Copy(ClipboardHistoryItem item)
    {
        try
        {
            _adapter.SetUnicodeText(item.Text);
            _selfSequence = _adapter.GetSequenceNumber();
            Notice?.Invoke(this, "已复制到剪贴板。");
        }
        catch (Exception exception) when (exception is ExternalException or COMException or InvalidOperationException)
        {
            Notice?.Invoke(this, "复制失败，请重试。");
        }
    }

    public void Delete(string id)
    {
        _items.RemoveAll(item => item.Id == id);
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    public void SetPinned(string id, bool pinned)
    {
        ClipboardHistoryItem? item = _items.FirstOrDefault(item => item.Id == id);
        if (item is null)
            return;
        item.IsPinned = pinned;
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    public void Clear()
    {
        _items.Clear();
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    public void SetCapacity(int capacity)
    {
        _settings.Settings.ClipboardPlusCapacity = ClipboardOptions.Capacities.Contains(capacity)
            ? capacity : ClipboardOptions.DefaultCapacity;
        Prune();
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    public void SetRetentionDays(int days)
    {
        _settings.Settings.ClipboardPlusRetentionDays = ClipboardOptions.RetentionDays.Contains(days)
            ? days : ClipboardOptions.DefaultRetentionDays;
        Prune();
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    private void Listener_ClipboardChanged(object? sender, ClipboardChangedEventArgs e)
    {
        if (e.SequenceNumber != 0 && e.SequenceNumber == _selfSequence)
            return;
        if (!ClipboardOptions.IsValidText(e.Text))
            return;

        ClipboardSource source = _sourceResolver.Resolve(e.OwnerWindow);
        if (IsExcluded(source))
            return;

        ClipboardHistoryItem? existing = _items.FirstOrDefault(item => item.Text == e.Text);
        if (existing is not null)
        {
            _items.Remove(existing);
            existing.CapturedAt = DateTimeOffset.Now;
        }
        else
        {
            existing = new ClipboardHistoryItem
            {
                Text = e.Text,
                CapturedAt = DateTimeOffset.Now,
                SourceProcessName = source.ProcessName,
                SourcePath = source.ExecutablePath
            };
        }

        _items.Insert(0, existing);
        Prune();
        Changed?.Invoke(this, EventArgs.Empty);
        ScheduleSave();
    }

    private bool IsExcluded(ClipboardSource source)
    {
        IReadOnlyList<string> paths = _settings.Settings.ClipboardPlusExcludedPaths;
        if (!string.IsNullOrWhiteSpace(source.ExecutablePath) &&
            paths.Any(path => string.Equals(path, source.ExecutablePath, StringComparison.OrdinalIgnoreCase)))
            return true;

        return !string.IsNullOrWhiteSpace(source.ProcessName) &&
            _settings.Settings.ClipboardPlusExcludedProcessNames.Any(name =>
                string.Equals(name, source.ProcessName, StringComparison.OrdinalIgnoreCase));
    }

    private void Prune()
    {
        int retention = _settings.Settings.ClipboardPlusRetentionDays;
        if (retention > 0)
        {
            DateTimeOffset cutoff = DateTimeOffset.Now.AddDays(-retention);
            _items.RemoveAll(item => !item.IsPinned && item.CapturedAt < cutoff);
        }

        int capacity = _settings.Settings.ClipboardPlusCapacity;
        if (!ClipboardOptions.Capacities.Contains(capacity))
            capacity = ClipboardOptions.DefaultCapacity;

        for (int index = _items.Count - 1; _items.Count > capacity && index >= 0; index--)
        {
            if (_items[index].IsPinned)
                continue;
            _items.RemoveAt(index);
        }

        // If every item is pinned, keep them; pinning is an explicit user choice.
        while (_items.Count > capacity && _items.Any(item => !item.IsPinned))
            _items.RemoveAt(_items.FindLastIndex(item => !item.IsPinned));
    }

    private void ScheduleSave()
    {
        _saveCancellation?.Cancel();
        CancellationTokenSource source = _saveCancellation = new();
        ClipboardHistoryItem[] snapshot = _items.ToArray();
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(700, source.Token).ConfigureAwait(false);
                await _store.SaveAsync(snapshot, source.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { }
            catch (Exception) { Notice?.Invoke(this, "剪贴板历史保存失败，原有历史仍保留在内存中。"); }
        });
    }

    public void Dispose()
    {
        _saveCancellation?.Cancel();
        try { _store.SaveAsync(_items.ToArray()).GetAwaiter().GetResult(); } catch { }
        _listener.ClipboardChanged -= Listener_ClipboardChanged;
        _listener.Stop();
        _listener.Dispose();
    }
}
