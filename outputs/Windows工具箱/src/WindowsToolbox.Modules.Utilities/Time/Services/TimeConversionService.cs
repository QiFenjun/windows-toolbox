using System.Globalization;
using WindowsToolbox.Modules.Utilities.Time.Models;

namespace WindowsToolbox.Modules.Utilities.Time.Services;

public static class TimeConversionService
{
    public static DateTimeOffset FromUnixTimestamp(long value, UnixTimestampUnit unit) => unit switch
    {
        UnixTimestampUnit.Seconds => DateTimeOffset.FromUnixTimeSeconds(value),
        UnixTimestampUnit.Milliseconds => DateTimeOffset.FromUnixTimeMilliseconds(value),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    public static long ToUnixTimestamp(DateTimeOffset value, UnixTimestampUnit unit) => unit switch
    {
        UnixTimestampUnit.Seconds => value.ToUnixTimeSeconds(),
        UnixTimestampUnit.Milliseconds => value.ToUnixTimeMilliseconds(),
        _ => throw new ArgumentOutOfRangeException(nameof(unit))
    };

    public static string FormatIso(DateTimeOffset value) => value.ToString("O", CultureInfo.InvariantCulture);

    public static TimeSpan Difference(DateTimeOffset start, DateTimeOffset end) => end - start;

    public static TimeZoneResolution ResolveLocalTime(
        DateTime localTime,
        TimeZoneInfo sourceTimeZone,
        TimeSpan? ambiguousOffset = null)
    {
        ArgumentNullException.ThrowIfNull(sourceTimeZone);
        DateTime wallTime = DateTime.SpecifyKind(localTime, DateTimeKind.Unspecified);
        if (sourceTimeZone.IsInvalidTime(wallTime))
            return new(TimeZoneResolutionStatus.InvalidLocalTime, null, Array.Empty<TimeSpan>());

        TimeSpan offset;
        if (sourceTimeZone.IsAmbiguousTime(wallTime))
        {
            TimeSpan[] offsets = sourceTimeZone.GetAmbiguousTimeOffsets(wallTime);
            if (ambiguousOffset is null)
                return new(TimeZoneResolutionStatus.AmbiguousLocalTime, null, offsets);
            if (!offsets.Contains(ambiguousOffset.Value))
                return new(TimeZoneResolutionStatus.InvalidAmbiguousOffset, null, offsets);
            offset = ambiguousOffset.Value;
        }
        else
        {
            offset = sourceTimeZone.GetUtcOffset(wallTime);
        }

        try
        {
            return TimeZoneResolution.Success(new DateTimeOffset(wallTime, offset));
        }
        catch (ArgumentOutOfRangeException)
        {
            return new(TimeZoneResolutionStatus.InvalidInput, null, Array.Empty<TimeSpan>());
        }
    }

    public static TimeZoneResolution ConvertLocalTime(
        DateTime localTime,
        TimeZoneInfo sourceTimeZone,
        TimeZoneInfo targetTimeZone,
        TimeSpan? ambiguousOffset = null)
    {
        ArgumentNullException.ThrowIfNull(targetTimeZone);
        TimeZoneResolution source = ResolveLocalTime(localTime, sourceTimeZone, ambiguousOffset);
        return source.Status == TimeZoneResolutionStatus.Success
            ? source with { Value = TimeZoneInfo.ConvertTime(source.Value!.Value, targetTimeZone) }
            : source;
    }

    public static bool TryParseIso(
        string? input,
        TimeZoneInfo sourceTimeZone,
        TimeSpan? ambiguousOffset,
        out DateTimeOffset value,
        out TimeZoneResolutionStatus status,
        out IReadOnlyList<TimeSpan> ambiguousOffsets)
    {
        ArgumentNullException.ThrowIfNull(sourceTimeZone);
        value = default;
        ambiguousOffsets = Array.Empty<TimeSpan>();
        status = TimeZoneResolutionStatus.InvalidInput;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        string text = input.Trim();
        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTimeOffset offsetValue) &&
            HasExplicitOffset(text))
        {
            value = offsetValue;
            status = TimeZoneResolutionStatus.Success;
            return true;
        }

        if (!DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime wallTime))
            return false;

        TimeZoneResolution resolved = ResolveLocalTime(wallTime, sourceTimeZone, ambiguousOffset);
        status = resolved.Status;
        ambiguousOffsets = resolved.AmbiguousOffsets;
        if (resolved.Status != TimeZoneResolutionStatus.Success)
            return false;
        value = resolved.Value!.Value;
        return true;
    }

    private static bool HasExplicitOffset(string text)
    {
        if (text.EndsWith('Z') || text.EndsWith('z'))
            return true;
        int timeStart = text.IndexOfAny(['T', 't', ' ']);
        return timeStart >= 0 && text.AsSpan(timeStart + 1).IndexOfAny('+', '-') >= 0;
    }
}
