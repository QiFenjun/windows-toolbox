namespace WindowsToolbox.Modules.InstalledApps.Models;

public sealed record UninstallResult(
    bool Started,
    bool ProcessCompleted,
    int? ExitCode,
    string Message)
{
    public static UninstallResult Failed(string message) =>
        new(false, false, null, message);
}

public sealed record UninstallCommand(
    string ExecutablePath,
    string Arguments);
