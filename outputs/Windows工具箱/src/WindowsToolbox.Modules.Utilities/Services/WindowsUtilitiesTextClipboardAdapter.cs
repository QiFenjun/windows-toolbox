using System.Windows;

namespace WindowsToolbox.Modules.Utilities.Services;

public sealed class WindowsUtilitiesTextClipboardAdapter : IUtilitiesTextClipboardAdapter
{
    public void SetText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Clipboard.SetText(text, TextDataFormat.UnicodeText);
    }
}
