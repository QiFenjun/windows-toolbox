namespace WindowsToolbox.Modules.TextTools.Services;

public interface ITextClipboardAdapter
{
    string? ReadText();
    void WriteText(string text);
}
