namespace WindowsToolbox.Modules.Shutdown.Models;

public enum ShutdownError
{
    None,
    InvalidTime,
    PlanAlreadyExists,
    NoActivePlan,
    CommandFailed,
    PermissionDenied,
    SystemError
}

public sealed record ShutdownOperationResult(
    bool IsSuccess,
    string Message,
    ShutdownError Error = ShutdownError.None,
    DateTime? ScheduledTime = null)
{
    public static ShutdownOperationResult Success(string message, DateTime? scheduledTime = null) =>
        new(true, message, ShutdownError.None, scheduledTime);

    public static ShutdownOperationResult Failure(ShutdownError error, string message) =>
        new(false, message, error);
}
