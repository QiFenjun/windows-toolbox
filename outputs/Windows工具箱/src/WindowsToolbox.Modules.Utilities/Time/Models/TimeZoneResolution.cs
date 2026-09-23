namespace WindowsToolbox.Modules.Utilities.Time.Models;

public sealed record TimeZoneResolution(
    TimeZoneResolutionStatus Status,
    DateTimeOffset? Value,
    IReadOnlyList<TimeSpan> AmbiguousOffsets)
{
    public static TimeZoneResolution Success(DateTimeOffset value) =>
        new(TimeZoneResolutionStatus.Success, value, Array.Empty<TimeSpan>());
}
