using System.Windows;

namespace WindowsToolbox.Modules.TextTools.Services;

public sealed class WindowsTextClipboardAdapter : ITextClipboardAdapter
{
    public string? ReadText() => Clipboard.ContainsText(TextDataFormat.UnicodeText)
        ? Clipboard.GetText(TextDataFormat.UnicodeText)
        : null;

    public void WriteText(string text) => Clipboard.SetText(text, TextDataFormat.UnicodeText);
}
