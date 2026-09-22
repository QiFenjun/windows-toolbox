using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

public static class WindowSearch
{
    public static bool Matches(WindowSnapshot window, string? query)
    {
        string value = query?.Trim() ?? string.Empty;
        return string.IsNullOrWhiteSpace(value) ||
            window.Title.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
            window.ProcessName.Contains(value, StringComparison.CurrentCultureIgnoreCase) ||
            window.ProcessId.ToString().Contains(value, StringComparison.Ordinal);
    }
}
