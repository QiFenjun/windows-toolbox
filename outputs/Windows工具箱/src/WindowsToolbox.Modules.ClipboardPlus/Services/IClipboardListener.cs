namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed record ClipboardChangedEventArgs(string Text, uint SequenceNumber, nint OwnerWindow);

public interface IClipboardListener : IDisposable
{
    event EventHandler<ClipboardChangedEventArgs>? ClipboardChanged;
    event EventHandler? HotkeyPressed;
    bool IsRunning { get; }
    bool IsHotkeyRegistered { get; }
    bool Start(bool registerHotkey = true);
    void Stop();
}
