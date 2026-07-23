using WindowsToolbox.Core.Models;

namespace WindowsToolbox.Core.Interfaces;

public interface IThemeService
{
    ThemeMode CurrentMode { get; }
    void Apply(ThemeMode mode);
}
