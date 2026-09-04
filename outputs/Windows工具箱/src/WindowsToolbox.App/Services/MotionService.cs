using WindowsToolbox.Core.Interfaces;
using WindowsToolbox.Core.Models;

namespace WindowsToolbox.App.Services;

public sealed class MotionService(ReducedMotionMode initialMode) : IMotionService
{
    public ReducedMotionMode CurrentMode { get; private set; } = initialMode;

    public event EventHandler? Changed;

    public void SetMode(ReducedMotionMode mode)
    {
        if (CurrentMode == mode)
            return;

        CurrentMode = mode;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public TimeSpan GetDuration(TimeSpan fullDuration) => CurrentMode switch
    {
        ReducedMotionMode.Off => TimeSpan.Zero,
        ReducedMotionMode.Reduced => TimeSpan.FromTicks(fullDuration.Ticks / 2),
        _ => fullDuration
    };
}
