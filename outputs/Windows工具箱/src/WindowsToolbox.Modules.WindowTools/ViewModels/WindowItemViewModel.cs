using System.Windows.Media;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.ViewModels;

public sealed class WindowItemViewModel(WindowSnapshot snapshot) : ObservableObject
{
    public WindowSnapshot Snapshot { get; } = snapshot;
    public nint Hwnd => Snapshot.Hwnd;
    public string Title => Snapshot.Title;
    public uint ProcessId => Snapshot.ProcessId;
    public string ProcessName => Snapshot.ProcessName;
    public string? ExecutablePath => Snapshot.ExecutablePath;
    public string ClassName => Snapshot.ClassName;
    public WindowRect WindowRect => Snapshot.WindowRect;
    public WindowRect ClientRect => Snapshot.ClientRect;
    public string HwndText => Snapshot.HwndText;
    public string ProcessText => $"{ProcessName} · PID {ProcessId}";
    public string SizeText => $"{WindowRect.Width} × {WindowRect.Height} · {Snapshot.MonitorName}";
    public string StateText => Snapshot.StateText;
    public string MonitorText => Snapshot.MonitorName;
    public string DpiText => $"{Snapshot.Dpi} DPI";
    public string TopMostText => Snapshot.IsTopMost ? "是" : "否";
    public bool IsTopMost => Snapshot.IsTopMost;
    public bool IsModifiable => Snapshot.IsModifiable;
    public ImageSource? Icon { get; set; }
    public string FallbackGlyph => "\uE8A7";

    public void RefreshIcon() => OnPropertyChanged(nameof(Icon));
}
