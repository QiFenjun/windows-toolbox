namespace WindowsToolbox.Modules.Utilities.Time.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
