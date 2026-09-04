namespace WindowsToolbox.Core.Models;

public enum ThemeMode
{
    System,
    Light,
    Dark
}

public sealed class AppSettings
{
    public ThemeMode Theme { get; set; } = ThemeMode.System;
    public string StartupPageId { get; set; } = "home";
    public bool ConfirmOperations { get; set; } = true;
    public bool RememberSidebarExpanded { get; set; } = true;
    public bool NetworkTrafficAutoStart { get; set; } = true;
    public bool NetworkTrafficContinueInBackground { get; set; }
    public bool NetworkTrafficStartWithWindows { get; set; }
    public bool IsSidebarExpanded { get; set; } = true;
    public ReducedMotionMode ReducedMotion { get; set; } = ReducedMotionMode.Full;
    public List<string> RecentModuleIds { get; set; } = [];
}
