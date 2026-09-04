using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Core.Interfaces;

/// <summary>Provides one reduced-motion policy for all UI transitions.</summary>
public interface IMotionService
{
    ReducedMotionMode CurrentMode { get; }
    event EventHandler? Changed;
    void SetMode(ReducedMotionMode mode);
    TimeSpan GetDuration(TimeSpan fullDuration);
}
