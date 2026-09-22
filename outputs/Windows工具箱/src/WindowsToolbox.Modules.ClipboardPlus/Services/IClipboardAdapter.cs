namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public interface IClipboardAdapter
{
    bool ContainsUnicodeText();
    string? GetUnicodeText();
    void SetUnicodeText(string text);
    uint GetSequenceNumber();
    nint GetOwnerWindow();
}
