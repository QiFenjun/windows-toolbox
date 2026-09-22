using System.Windows.Media;

namespace WindowsToolbox.Modules.WindowTools.Services;

public interface IWindowIconService
{
    Task<ImageSource?> GetAsync(string? executablePath, CancellationToken cancellationToken = default);
}
