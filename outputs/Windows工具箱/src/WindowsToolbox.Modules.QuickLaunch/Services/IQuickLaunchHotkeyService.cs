namespace WindowsToolbox.Modules.QuickLaunch.Services;

public interface IQuickLaunchHotkeyService : IDisposable
{
    bool IsRegistered { get; }
    event EventHandler? Pressed;
    bool Start(bool enabled = true);
    void Stop();
}
