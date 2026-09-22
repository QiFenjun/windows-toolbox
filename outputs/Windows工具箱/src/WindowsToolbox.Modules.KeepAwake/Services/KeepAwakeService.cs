using System.Diagnostics;
using WindowsToolbox.Modules.KeepAwake.Models;

namespace WindowsToolbox.Modules.KeepAwake.Services;

public sealed class KeepAwakeService(IExecutionStatePlatform platform, TimeProvider? timeProvider = null) : IDisposable
{
    private readonly object _gate = new();
    private readonly TimeProvider _clock = timeProvider ?? TimeProvider.System;
    private Run? _run;
    private Thread? _lastWorker;
    private KeepAwakeState _state;
    private KeepAwakeMode _mode;
    private DateTimeOffset? _startedAt, _expiresAt;
    private KeepAwakeEndReason _endReason;
    private int _error;
    private bool _disposed;

    public KeepAwakeSnapshot Snapshot
    {
        get
        {
            lock (_gate) return new(_state, _run is not null, _mode, _startedAt, _expiresAt,
                _run is { Duration: not null } run ? Remaining(run) : null, _endReason, _error);
        }
    }

    public Task<bool> StartAsync(KeepAwakeMode mode, TimeSpan? duration)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (duration is { } time && (time <= TimeSpan.Zero || time > TimeSpan.FromDays(1))) throw new ArgumentOutOfRangeException(nameof(duration));
        lock (_gate)
        {
            if (_disposed || _run is not null) return Task.FromResult(false);
            Run run = new(mode, duration);
            _run = run; _mode = mode; _endReason = KeepAwakeEndReason.None; _error = 0;
            run.Thread = new Thread(() => Work(run)) { IsBackground = true, Name = "WindowsToolbox.KeepAwake" };
            _lastWorker = run.Thread;
            run.Thread.SetApartmentState(ApartmentState.MTA);
            run.Thread.Start();
            return run.Started.Task;
        }
    }

    public async Task StopAsync()
    {
        Run? run;
        Thread? worker;
        lock (_gate)
        {
            run = _run;
            worker = _lastWorker;
            if (run is not null)
            {
                _state = KeepAwakeState.Stopping;
                run.Stop.Set();
            }
        }
        if (run is not null) await run.Finished.Task.ConfigureAwait(false);
        worker?.Join(); // Also join a just-expired/failed worker after it cleared _run.
    }

    private TimeSpan Remaining(Run run) => TimeSpan.FromTicks(Math.Max(0,
        (run.Duration!.Value - _clock.GetElapsedTime(run.StartTimestamp)).Ticks));

    private void Work(Run run)
    {
        KeepAwakeEndReason reason = KeepAwakeEndReason.Stopped;
        int error = 0;
        try
        {
            ExecutionState flags = ExecutionState.Continuous | ExecutionState.SystemRequired;
            if (run.Mode == KeepAwakeMode.SystemAndDisplay) flags |= ExecutionState.DisplayRequired;
            ExecutionStateResult activation = platform.Set(flags);
            if (!activation.Succeeded)
            {
                reason = KeepAwakeEndReason.ActivationFailed; error = activation.Win32Error;
                return;
            }
            lock (_gate)
            {
                run.StartTimestamp = _clock.GetTimestamp();
                _startedAt = _clock.GetUtcNow(); _expiresAt = run.Duration is { } duration ? _startedAt + duration : null;
                _state = run.Stop.IsSet ? KeepAwakeState.Stopping : KeepAwakeState.Active;
            }
            run.Started.TrySetResult(true);
            // Only wait/count down here. No periodic Windows execution-state calls.
            while (!run.Stop.IsSet)
            {
                TimeSpan? remaining = run.Duration is null ? null : Remaining(run);
                if (remaining <= TimeSpan.Zero) { reason = KeepAwakeEndReason.Expired; break; }
                run.Stop.Wait(remaining is null ? Timeout.InfiniteTimeSpan : TimeSpan.FromMilliseconds(Math.Min(1000, remaining.Value.TotalMilliseconds)));
            }
        }
        catch (Exception ex)
        {
            reason = KeepAwakeEndReason.ActivationFailed;
            error = ex is System.ComponentModel.Win32Exception native ? native.NativeErrorCode : 0;
        }
        finally
        {
            try
            {
                // Always on the SAME owning thread, including failed activation and shutdown races.
                ExecutionStateResult release = platform.Set(ExecutionState.Continuous);
                if (!release.Succeeded) { reason = KeepAwakeEndReason.ReleaseFailed; error = release.Win32Error; }
            }
            catch (Exception) { reason = KeepAwakeEndReason.ReleaseFailed; }
            if (reason is KeepAwakeEndReason.ActivationFailed or KeepAwakeEndReason.ReleaseFailed)
                Trace.TraceError("KeepAwake {0}; Win32Error={1} (0=unavailable)", reason, error);
            lock (_gate)
            {
                _state = KeepAwakeState.Inactive; _endReason = reason; _error = error;
                _run = null;
                run.Stop.Dispose();
            }
            run.Started.TrySetResult(false);
            run.Finished.TrySetResult();
            // Exiting the owner thread is also the fallback if explicit native release failed.
        }
    }

    public void Dispose()
    {
        lock (_gate) _disposed = true;
        StopAsync().GetAwaiter().GetResult();
    }

    private sealed class Run(KeepAwakeMode mode, TimeSpan? duration)
    {
        public KeepAwakeMode Mode { get; } = mode;
        public TimeSpan? Duration { get; } = duration;
        public long StartTimestamp;
        public Thread? Thread;
        public ManualResetEventSlim Stop { get; } = new(false);
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
