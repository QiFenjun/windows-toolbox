using WindowsToolbox.Modules.Utilities.Time.Models;
using WindowsToolbox.Modules.Utilities.Time.Services;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class TimeToolsCoreTests
{
    [TestMethod]
    public void UnixTimestamp_UsesUtcEpochAndSupportsNegativeSeconds()
    {
        Assert.AreEqual(DateTimeOffset.UnixEpoch, TimeConversionService.FromUnixTimestamp(0, UnixTimestampUnit.Seconds));
        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddSeconds(-1), TimeConversionService.FromUnixTimestamp(-1, UnixTimestampUnit.Seconds));
        Assert.AreEqual(-1, TimeConversionService.ToUnixTimestamp(DateTimeOffset.UnixEpoch.AddSeconds(-1), UnixTimestampUnit.Seconds));
    }

    [TestMethod]
    public void UnixTimestamp_SupportsSecondsMillisecondsAndModernDates()
    {
        DateTimeOffset before2038 = new(2037, 12, 31, 23, 59, 59, TimeSpan.Zero);
        Assert.AreEqual(before2038, TimeConversionService.FromUnixTimestamp(
            TimeConversionService.ToUnixTimestamp(before2038, UnixTimestampUnit.Seconds), UnixTimestampUnit.Seconds));

        DateTimeOffset modern = new(2026, 9, 23, 6, 32, 18, 123, TimeSpan.Zero);
        long milliseconds = TimeConversionService.ToUnixTimestamp(modern, UnixTimestampUnit.Milliseconds);
        Assert.AreEqual(1790145138123L, milliseconds);
        Assert.AreEqual(modern, TimeConversionService.FromUnixTimestamp(milliseconds, UnixTimestampUnit.Milliseconds));
    }

    [TestMethod]
    public void UnixTimestamp_RejectsValuesOutsideDateTimeOffsetRange()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimeConversionService.FromUnixTimestamp(long.MaxValue, UnixTimestampUnit.Seconds));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimeConversionService.FromUnixTimestamp(long.MinValue, UnixTimestampUnit.Milliseconds));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimeConversionService.FromUnixTimestamp(253402300800000L, UnixTimestampUnit.Milliseconds));
        Assert.ThrowsException<ArgumentOutOfRangeException>(() => TimeConversionService.FromUnixTimestamp(1, (UnixTimestampUnit)99));
    }

    [DataTestMethod]
    [DataRow("2026-09-23T12:30:45Z", 0)]
    [DataRow("2026-09-23T12:30:45+08:00", 480)]
    [DataRow("2026-09-23T12:30:45-05:30", -330)]
    [DataRow("2026-09-23T12:30:45.123+09:00", 540)]
    public void IsoParser_PreservesExplicitOffsets(string input, int expectedMinutes)
    {
        bool parsed = TimeConversionService.TryParseIso(input, TimeZoneInfo.Utc, null,
            out DateTimeOffset value, out TimeZoneResolutionStatus status, out _);

        Assert.IsTrue(parsed);
        Assert.AreEqual(TimeZoneResolutionStatus.Success, status);
        Assert.AreEqual(TimeSpan.FromMinutes(expectedMinutes), value.Offset);
        Assert.IsTrue(TimeConversionService.TryParseIso(TimeConversionService.FormatIso(value), TimeZoneInfo.Utc, null,
            out DateTimeOffset roundTrip, out _, out _));
        Assert.IsTrue(value.EqualsExact(roundTrip));
    }

    [TestMethod]
    public void IsoParser_RejectsInvalidInputAndUsesSelectedZoneWhenOffsetIsAbsent()
    {
        Assert.IsFalse(TimeConversionService.TryParseIso("not a date", TimeZoneInfo.Utc, null,
            out _, out TimeZoneResolutionStatus invalidStatus, out _));
        Assert.AreEqual(TimeZoneResolutionStatus.InvalidInput, invalidStatus);

        TimeZoneInfo eastern = EasternTimeZone();
        Assert.IsTrue(TimeConversionService.TryParseIso("2024-01-15T12:00:00", eastern, null,
            out DateTimeOffset value, out TimeZoneResolutionStatus status, out _));
        Assert.AreEqual(TimeZoneResolutionStatus.Success, status);
        Assert.AreEqual(TimeSpan.FromHours(-5), value.Offset);
    }

    [TestMethod]
    public void IsoParser_RequiresAChoiceForAmbiguousLocalTime()
    {
        TimeZoneInfo eastern = EasternTimeZone();
        Assert.IsFalse(TimeConversionService.TryParseIso("2024-11-03T01:30:00", eastern, null,
            out _, out TimeZoneResolutionStatus status, out IReadOnlyList<TimeSpan> offsets));
        Assert.AreEqual(TimeZoneResolutionStatus.AmbiguousLocalTime, status);
        CollectionAssert.AreEquivalent(new[] { TimeSpan.FromHours(-4), TimeSpan.FromHours(-5) }, offsets.ToArray());

        Assert.IsTrue(TimeConversionService.TryParseIso("2024-11-03T01:30:00", eastern, TimeSpan.FromHours(-4),
            out DateTimeOffset selected, out _, out _));
        Assert.AreEqual(TimeSpan.FromHours(-4), selected.Offset);
    }

    [TestMethod]
    public void TimeZoneConversion_UsesSystemRulesAndRejectsInvalidOrAmbiguousWallTimes()
    {
        TimeZoneInfo eastern = EasternTimeZone();
        DateTime winter = new(2024, 1, 15, 12, 0, 0);
        DateTime summer = new(2024, 7, 15, 12, 0, 0);
        TimeZoneResolution winterResult = TimeConversionService.ConvertLocalTime(winter, eastern, TimeZoneInfo.Utc);
        TimeZoneResolution summerResult = TimeConversionService.ConvertLocalTime(summer, eastern, TimeZoneInfo.Utc);
        Assert.AreEqual(new DateTimeOffset(2024, 1, 15, 17, 0, 0, TimeSpan.Zero), winterResult.Value);
        Assert.AreEqual(new DateTimeOffset(2024, 7, 15, 16, 0, 0, TimeSpan.Zero), summerResult.Value);

        DateTime invalid = new(2024, 3, 10, 2, 30, 0);
        Assert.AreEqual(TimeZoneResolutionStatus.InvalidLocalTime,
            TimeConversionService.ResolveLocalTime(invalid, eastern).Status);

        DateTime ambiguous = new(2024, 11, 3, 1, 30, 0);
        Assert.AreEqual(TimeZoneResolutionStatus.AmbiguousLocalTime,
            TimeConversionService.ResolveLocalTime(ambiguous, eastern).Status);
        Assert.AreEqual(TimeZoneResolutionStatus.InvalidAmbiguousOffset,
            TimeConversionService.ResolveLocalTime(ambiguous, eastern, TimeSpan.Zero).Status);
        Assert.AreEqual(TimeSpan.FromHours(-5), TimeConversionService.ResolveLocalTime(
            ambiguous, eastern, TimeSpan.FromHours(-5)).Value!.Value.Offset);
    }

    [TestMethod]
    public void TimeZoneConversion_PreservesInstantForUtcSameZoneAndTargetConversion()
    {
        TimeZoneInfo eastern = EasternTimeZone();
        DateTime local = new(2024, 1, 15, 12, 0, 0);
        TimeZoneResolution source = TimeConversionService.ResolveLocalTime(local, eastern);
        TimeZoneResolution same = TimeConversionService.ConvertLocalTime(local, eastern, eastern);
        TimeZoneResolution converted = TimeConversionService.ConvertLocalTime(local, eastern, TimeZoneInfo.Utc);

        Assert.AreEqual(source.Value, same.Value);
        Assert.AreEqual(source.Value!.Value.UtcDateTime, converted.Value!.Value.UtcDateTime);
        Assert.AreEqual(TimeSpan.Zero, converted.Value.Value.Offset);

        TimeZoneResolution fromUtc = TimeConversionService.ConvertLocalTime(
            new DateTime(2024, 1, 15, 17, 0, 0), TimeZoneInfo.Utc, eastern);
        Assert.AreEqual(new DateTimeOffset(2024, 1, 15, 12, 0, 0, TimeSpan.FromHours(-5)), fromUtc.Value);
    }

    [TestMethod]
    public void TimeZoneCatalog_CachesSystemZonesAndSearchesIdAndNames()
    {
        Assert.IsTrue(TimeZoneCatalog.SystemZones.Count > 0);
        Assert.AreSame(TimeZoneCatalog.SystemZones, TimeZoneCatalog.SystemZones);
        TimeZoneInfo eastern = EasternTimeZone();
        Assert.IsTrue(TimeZoneCatalog.Search(eastern.Id).Contains(eastern));
        Assert.IsTrue(TimeZoneCatalog.Search(eastern.DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]).Contains(eastern));
        Assert.AreEqual(TimeZoneCatalog.SystemZones.Count, TimeZoneCatalog.Search(null).Count);
    }

    [TestMethod]
    public void Duration_PreservesPositiveNegativeAndZeroElapsedTimeAcrossOffsets()
    {
        DateTimeOffset start = new(2024, 3, 10, 1, 30, 0, TimeSpan.FromHours(-5));
        DateTimeOffset end = new(2024, 3, 10, 3, 30, 0, TimeSpan.FromHours(-4));
        Assert.AreEqual(TimeSpan.FromHours(1), TimeConversionService.Difference(start, end));
        Assert.AreEqual(TimeSpan.FromHours(-1), TimeConversionService.Difference(end, start));
        Assert.AreEqual(TimeSpan.Zero, TimeConversionService.Difference(start, start));
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), TimeConversionService.Difference(start, start.AddMilliseconds(250)));
        Assert.AreEqual(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1),
            TimeConversionService.Difference(start, start.AddDays(1).AddSeconds(1)));
    }

    [TestMethod]
    public void Clock_ProvidesUtcTimeForDeterministicConsumers()
    {
        IClock clock = new FixedClock(new DateTimeOffset(2026, 9, 23, 6, 32, 18, TimeSpan.Zero));
        Assert.AreEqual(DateTimeOffset.UnixEpoch.AddSeconds(1790145138), clock.UtcNow);
    }

    private static TimeZoneInfo EasternTimeZone()
    {
        foreach (string id in new[] { "Eastern Standard Time", "America/New_York" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
        }
        Assert.Fail("An Eastern Time zone with DST support is required for these tests.");
        throw new AssertFailedException("Unreachable.");
    }

    private sealed class FixedClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; } = utcNow;
    }
}
