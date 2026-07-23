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
    public bool IsSidebarExpanded { get; set; } = true;
    public List<string> RecentModuleIds { get; set; } = [];
}
