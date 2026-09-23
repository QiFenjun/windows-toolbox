using WindowsToolbox.Modules.Utilities.Services;
using WindowsToolbox.Modules.Utilities.Time.Services;
using WindowsToolbox.Modules.Utilities.Time.ViewModels;

namespace WindowsToolbox.Tests;

[TestClass]
public sealed class TimeToolsViewModelTests
{
    [TestMethod]
    public void CurrentTimeRefreshesOnlyWhileActiveAndDisposesTimer()
    {
        FakeClock clock = new(new DateTimeOffset(2026, 9, 23, 6, 32, 18, TimeSpan.Zero));
        FakeTimerFactory timers = new();
        FakeClipboard clipboard = new();
        TimeToolsViewModel viewModel = new(clock, timers, clipboard);

        Assert.AreEqual(0, timers.Timer.StartCount);
        Assert.AreEqual("1790145138", viewModel.CurrentUnixSeconds);
        Assert.AreEqual("1790145138000", viewModel.CurrentUnixMilliseconds);
        viewModel.TimestampUnitIndex = -1;
        Assert.AreEqual(0, viewModel.TimestampUnitIndex);
        Assert.AreEqual("2026-09-23 06:32:18Z", viewModel.CurrentUtcTime);
        Assert.IsTrue(DateTimeOffset.TryParse(viewModel.CurrentIso, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.None, out DateTimeOffset currentIso));
        Assert.IsTrue(currentIso.EqualsExact(clock.UtcNow.ToLocalTime()));
        Assert.AreEqual(clock.UtcNow.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss zzz", System.Globalization.CultureInfo.InvariantCulture),
            viewModel.CurrentLocalTime);
        viewModel.Activate();
        Assert.AreEqual(1, timers.Timer.StartCount);
        Assert.IsTrue(viewModel.IsActive);

        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        timers.Timer.Tick();
        Assert.AreEqual("1790145139", viewModel.CurrentUnixSeconds);

        viewModel.Deactivate();
        Assert.AreEqual(1, timers.Timer.StopCount);
        Assert.IsFalse(viewModel.IsActive);
        clock.UtcNow = clock.UtcNow.AddSeconds(1);
        timers.Timer.Tick();
        Assert.AreEqual("1790145139", viewModel.CurrentUnixSeconds);

        viewModel.Dispose();
        viewModel.Dispose();
        Assert.AreEqual(1, timers.Timer.DisposeCount);
    }

    [TestMethod]
    public void TimestampViewModelConvertsBothDirectionsAndReportsRangeErrors()
    {
        using TimeToolsViewModel viewModel = CreateViewModel();

        viewModel.TimestampText = "-1";
        viewModel.ConvertTimestampCommand.Execute(null);
        Assert.AreEqual("1969-12-31 23:59:59 +00:00", viewModel.ConvertedUtc);
        Assert.AreEqual("1969-12-31T23:59:59.0000000+00:00", viewModel.ConvertedIso);

        viewModel.TimestampText = "not a number";
        viewModel.ConvertTimestampCommand.Execute(null);
        Assert.AreEqual("请输入有效的整数时间戳。", viewModel.TimestampStatus);

        viewModel.TimestampText = long.MaxValue.ToString(System.Globalization.CultureInfo.InvariantCulture);
        viewModel.ConvertTimestampCommand.Execute(null);
        Assert.AreEqual("时间戳超出支持范围。", viewModel.TimestampStatus);

        viewModel.DateTimeInput = "1970-01-01T00:00:00Z";
        viewModel.ConvertDateTimeCommand.Execute(null);
        Assert.AreEqual("0", viewModel.UnixSeconds);
        Assert.AreEqual("0", viewModel.UnixMilliseconds);
    }

    [TestMethod]
    public void DateTimeInputUsesSelectedZoneAndRequiresChoiceForAmbiguousDst()
    {
        using TimeToolsViewModel viewModel = CreateViewModel();
        TimeZoneInfo eastern = EasternTimeZone();
        viewModel.SourceTimeZone = eastern;
        viewModel.DateTimeInput = "2024-11-03T01:30:00";
        viewModel.ConvertDateTimeCommand.Execute(null);

        Assert.AreEqual("该时间在当前时区存在两个可能的 UTC Offset，请选择一个。", viewModel.DateTimeStatus);
        CollectionAssert.AreEquivalent(new[] { TimeSpan.FromHours(-4), TimeSpan.FromHours(-5) },
            viewModel.DateTimeAmbiguousOffsets.ToArray());

        viewModel.SelectedDateTimeAmbiguousOffset = TimeSpan.FromHours(-4);
        Assert.AreEqual(string.Empty, viewModel.DateTimeStatus);
        Assert.AreEqual("1730611800", viewModel.UnixSeconds);

        viewModel.DateTimeInput = "2024-03-10T02:30:00";
        viewModel.SelectedDateTimeAmbiguousOffset = null;
        viewModel.ConvertDateTimeCommand.Execute(null);
        Assert.AreEqual("该本地时间因夏令时切换而不存在。", viewModel.DateTimeStatus);
    }

    [TestMethod]
    public void TimeZoneViewModelUsesDstRulesAndExplicitlyResolvesRepeatedTime()
    {
        using TimeToolsViewModel viewModel = CreateViewModel();
        viewModel.SourceTimeZone = EasternTimeZone();
        viewModel.TargetTimeZone = TimeZoneInfo.Utc;
        viewModel.TimeZoneInput = "2024-07-15T12:00:00";
        viewModel.ConvertTimeZoneCommand.Execute(null);
        Assert.AreEqual("2024-07-15 16:00:00 +00:00", viewModel.TimeZoneTarget);

        viewModel.TimeZoneInput = "2024-03-10T02:30:00";
        viewModel.ConvertTimeZoneCommand.Execute(null);
        Assert.AreEqual("该本地时间因夏令时切换而不存在。", viewModel.TimeZoneStatus);

        viewModel.TimeZoneInput = "2024-11-03T01:30:00";
        viewModel.ConvertTimeZoneCommand.Execute(null);
        Assert.IsTrue(viewModel.HasTimeZoneAmbiguousOffsets);
        viewModel.SelectedTimeZoneAmbiguousOffset = TimeSpan.FromHours(-4);
        Assert.AreEqual("2024-11-03 05:30:00Z", viewModel.TimeZoneUtc);
    }

    [TestMethod]
    public void DifferenceViewModelPreservesSignAndCopiesPlainText()
    {
        FakeClipboard clipboard = new();
        using TimeToolsViewModel viewModel = CreateViewModel(clipboard);
        viewModel.StartInput = "2024-03-10T01:30:00-05:00";
        viewModel.EndInput = "2024-03-10T03:30:00-04:00";
        viewModel.CalculateDifferenceCommand.Execute(null);
        Assert.AreEqual("01:00:00", viewModel.DurationDisplay);
        Assert.AreEqual("3600", viewModel.DurationTotalSeconds);
        Assert.AreEqual("3600000", viewModel.DurationTotalMilliseconds);

        viewModel.StartInput = "2024-03-10T03:30:00-04:00";
        viewModel.EndInput = "2024-03-10T01:30:00-05:00";
        viewModel.CalculateDifferenceCommand.Execute(null);
        Assert.AreEqual("-01:00:00", viewModel.DurationDisplay);
        viewModel.CopyTextCommand.Execute(viewModel.DurationDisplay);
        Assert.AreEqual("-01:00:00", clipboard.Text);
    }

    [TestMethod]
    public void TimeZoneSearchFiltersOnDisplayStandardNameAndId()
    {
        using TimeToolsViewModel viewModel = CreateViewModel();
        TimeZoneInfo eastern = EasternTimeZone();
        viewModel.SourceZoneSearch = eastern.Id;
        Assert.IsTrue(viewModel.SourceTimeZones.Contains(eastern));
        Assert.IsTrue(viewModel.SourceTimeZones.All(zone => zone == eastern || zone == viewModel.SourceTimeZone ||
            zone.Id.Contains(eastern.Id, StringComparison.OrdinalIgnoreCase)));
    }

    private static TimeToolsViewModel CreateViewModel(FakeClipboard? clipboard = null) =>
        new(new FakeClock(new DateTimeOffset(2026, 9, 23, 6, 32, 18, TimeSpan.Zero)), new FakeTimerFactory(), clipboard ?? new FakeClipboard());

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

    private sealed class FakeClock(DateTimeOffset utcNow) : IClock
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;
    }

    private sealed class FakeTimerFactory : ITimeRefreshTimerFactory
    {
        public FakeTimer Timer { get; } = new();
        public ITimeRefreshTimer Create(TimeSpan interval, Action tick)
        {
            Assert.AreEqual(TimeSpan.FromSeconds(1), interval);
            Timer.Callback = tick;
            return Timer;
        }
    }

    private sealed class FakeTimer : ITimeRefreshTimer
    {
        public Action? Callback { get; set; }
        public bool IsRunning { get; private set; }
        public int StartCount { get; private set; }
        public int StopCount { get; private set; }
        public int DisposeCount { get; private set; }
        public void Start() { StartCount++; IsRunning = true; }
        public void Stop() { StopCount++; IsRunning = false; }
        public void Dispose() => DisposeCount++;
        public void Tick() { if (IsRunning) Callback?.Invoke(); }
    }

    private sealed class FakeClipboard : IUtilitiesTextClipboardAdapter
    {
        public string? Text { get; private set; }
        public void SetText(string text) => Text = text;
    }
}
