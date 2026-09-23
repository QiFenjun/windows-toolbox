namespace WindowsToolbox.Modules.Utilities.Color.Models;

// Win32 physical screen pixels (GetCursorPos), never WPF device-independent units.
public readonly record struct ScreenPoint(int X, int Y);
