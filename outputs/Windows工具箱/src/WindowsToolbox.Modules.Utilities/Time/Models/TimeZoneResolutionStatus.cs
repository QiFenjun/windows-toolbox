namespace WindowsToolbox.Modules.Utilities.Time.Models;

public enum TimeZoneResolutionStatus
{
    Success,
    InvalidInput,
    InvalidLocalTime,
    AmbiguousLocalTime,
    InvalidAmbiguousOffset
}
