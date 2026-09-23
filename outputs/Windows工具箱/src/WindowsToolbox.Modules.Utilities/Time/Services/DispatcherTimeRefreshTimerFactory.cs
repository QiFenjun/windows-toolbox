using System.Windows.Threading;

namespace WindowsToolbox.Modules.Utilities.Time.Services;

public sealed class DispatcherTimeRefreshTimerFactory(Dispatcher dispatcher) : ITimeRefreshTimerFactory
{
    public ITimeRefreshTimer Create(TimeSpan interval, Action tick) => new Timer(dispatcher, interval, tick);

    private sealed class Timer : ITimeRefreshTimer
    {
        private readonly DispatcherTimer _timer;
        private readonly EventHandler _handler;

        public Timer(Dispatcher dispatcher, TimeSpan interval, Action tick)
        {
            ArgumentNullException.ThrowIfNull(tick);
            _handler = (_, _) => tick();
            _timer = new DispatcherTimer(interval, DispatcherPriority.Background, _handler, dispatcher);
        }

        public void Start() => _timer.Start();
        public void Stop() => _timer.Stop();

        public void Dispose()
        {
            _timer.Stop();
            _timer.Tick -= _handler;
        }
    }
}
