namespace WindowsToolbox.Modules.Utilities.Time.Services;

public interface ITimeRefreshTimer : IDisposable
{
    void Start();
    void Stop();
}

public interface ITimeRefreshTimerFactory
{
    ITimeRefreshTimer Create(TimeSpan interval, Action tick);
}
