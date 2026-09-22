using System.Runtime.InteropServices;
using System.Windows;

namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed class WindowsClipboardAdapter : IClipboardAdapter
{
    public bool ContainsUnicodeText() => Clipboard.ContainsText(TextDataFormat.UnicodeText);

    public string? GetUnicodeText() =>
        ContainsUnicodeText() ? Clipboard.GetText(TextDataFormat.UnicodeText) : null;

    public void SetUnicodeText(string text) => Clipboard.SetText(text, TextDataFormat.UnicodeText);

    public uint GetSequenceNumber() => GetClipboardSequenceNumber();

    public nint GetOwnerWindow() => GetClipboardOwner();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint GetClipboardSequenceNumber();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetClipboardOwner();
}
