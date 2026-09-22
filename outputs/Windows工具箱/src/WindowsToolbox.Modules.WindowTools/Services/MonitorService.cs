using WindowsToolbox.Modules.WindowTools.Models;

namespace WindowsToolbox.Modules.WindowTools.Services;

public sealed class MonitorService(IWindowPlatform platform) : IMonitorService
{
    public IReadOnlyList<MonitorSnapshot> GetMonitors() => platform.EnumerateMonitors();
}
