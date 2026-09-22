namespace WindowsToolbox.Modules.KeepAwake.Models;

public enum KeepAwakeMode { System, SystemAndDisplay }
public enum KeepAwakeState { Inactive, Active, Stopping }
public enum KeepAwakeEndReason { None, Stopped, Expired, ActivationFailed, ReleaseFailed }

[Flags]
public enum ExecutionState : uint
{
    SystemRequired = 0x1,
    DisplayRequired = 0x2,
    Continuous = 0x80000000
}

public readonly record struct ExecutionStateResult(uint PreviousState, int Win32Error)
{
    public bool Succeeded => PreviousState != 0;
}

public sealed record KeepAwakeSnapshot(KeepAwakeState State, bool IsBusy, KeepAwakeMode Mode,
    DateTimeOffset? StartedAt, DateTimeOffset? ExpiresAt, TimeSpan? Remaining, KeepAwakeEndReason EndReason, int Win32Error);
public sealed record DurationOption(int Minutes, string Label)
{
    public override string ToString() => Label;
}
