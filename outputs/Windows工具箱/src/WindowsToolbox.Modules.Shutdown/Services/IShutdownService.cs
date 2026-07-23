using WindowsToolbox.Modules.Shutdown.Models;

namespace WindowsToolbox.Modules.Shutdown.Services;

public interface IShutdownService
{
    DateTime? ScheduledTime { get; }
    ShutdownOperationResult ValidateShutdownTime(DateTime shutdownTime);
    Task<ShutdownOperationResult> ScheduleShutdownAsync(DateTime shutdownTime, CancellationToken cancellationToken = default);
    Task<ShutdownOperationResult> CancelShutdownAsync(CancellationToken cancellationToken = default);
    TimeSpan? GetRemainingTime();
}
