using WindowsToolbox.Modules.ClipboardPlus.Models;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public interface IClipboardPlusService : IDisposable
{
    event EventHandler? Changed;
    event EventHandler<string>? Notice;
    event EventHandler? HotkeyPressed;
    IReadOnlyList<ClipboardHistoryItem> Items { get; }
    bool IsListening { get; }
    bool IsHotkeyRegistered { get; }
    Task LoadAsync();
    bool Start();
    void Stop();
    void Copy(ClipboardHistoryItem item);
    void Delete(string id);
    void SetPinned(string id, bool pinned);
    void Clear();
    void SetCapacity(int capacity);
    void SetRetentionDays(int days);
}
