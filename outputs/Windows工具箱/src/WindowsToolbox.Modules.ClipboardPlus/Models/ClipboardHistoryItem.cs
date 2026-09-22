using System.Text;
using WindowsToolbox.Core.Utilities;

namespace WindowsToolbox.Modules.ClipboardPlus.Models;

public sealed class ClipboardHistoryItem : ObservableObject
{
    private bool _isPinned;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = string.Empty;
    public DateTimeOffset CapturedAt { get; set; } = DateTimeOffset.Now;
    public bool IsPinned
    {
        get => _isPinned;
        set => SetProperty(ref _isPinned, value);
    }
    public string? SourceProcessName { get; set; }
    public string? SourcePath { get; set; }
    public string SourceDisplayName =>
        string.IsNullOrWhiteSpace(SourceProcessName) ? "未知来源" : SourceProcessName!;
    public string CapturedAtText => CapturedAt.LocalDateTime.ToString("yyyy-MM-dd HH:mm");
    public string SizeText
    {
        get
        {
            long bytes = Encoding.Unicode.GetByteCount(Text);
            return bytes < 1024 ? $"{bytes} B" : $"{bytes / 1024d:0.#} KB";
        }
    }
    public string Preview => Text.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ").Trim();
}
