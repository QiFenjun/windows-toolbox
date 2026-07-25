using System.Windows;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed class ApplicationClipboardService : IApplicationClipboardService
{
    public void CopyText(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
            Clipboard.SetText(text);
    }
}
