using WindowsToolbox.Modules.ClipboardPlus.Models;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public interface IClipboardHistoryStore
{
    string FilePath { get; }
    Task<IReadOnlyList<ClipboardHistoryItem>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyCollection<ClipboardHistoryItem> items, CancellationToken cancellationToken = default);
}
