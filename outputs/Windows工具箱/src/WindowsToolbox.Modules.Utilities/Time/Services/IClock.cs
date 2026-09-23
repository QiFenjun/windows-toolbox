namespace WindowsToolbox.Modules.Utilities.Time.Services;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
