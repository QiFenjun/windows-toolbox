namespace WindowsToolbox.Modules.ClipboardPlus.Services;

public sealed record ClipboardSource(string? ProcessName, string? ExecutablePath);

public interface IClipboardSourceResolver
{
    ClipboardSource Resolve(nint ownerWindow);
}
