using System.Windows.Media;

namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed record ApplicationIconResult(
    ImageSource? Icon,
    ApplicationIconSource Source,
    string? SourcePath)
{
    public static ApplicationIconResult Fallback { get; } = new(null, ApplicationIconSource.Default, null);
}
