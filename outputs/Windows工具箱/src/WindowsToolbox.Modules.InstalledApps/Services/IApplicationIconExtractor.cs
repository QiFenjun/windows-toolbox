using System.Windows.Media;

namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>隔离 Shell API，令缓存和并发行为可以独立测试。</summary>
public interface IApplicationIconExtractor
{
    ImageSource? Extract(string sourcePath, int? iconIndex, int desiredSize);
}
