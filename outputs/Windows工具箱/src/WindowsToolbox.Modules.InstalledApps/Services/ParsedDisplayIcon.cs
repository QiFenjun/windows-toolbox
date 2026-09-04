namespace WindowsToolbox.Modules.InstalledApps.Services;

/// <summary>卸载注册表 DisplayIcon 值解析后的图标资源位置。</summary>
public sealed record ParsedDisplayIcon(string Path, int? IconIndex);
