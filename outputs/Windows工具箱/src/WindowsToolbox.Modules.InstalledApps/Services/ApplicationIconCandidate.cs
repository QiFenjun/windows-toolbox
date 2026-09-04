namespace WindowsToolbox.Modules.InstalledApps.Services;

public sealed record ApplicationIconCandidate(
    string Path,
    int? IconIndex,
    ApplicationIconSource Source);
