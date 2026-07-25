using System.IO;
using WindowsToolbox.Core.Utilities;
using WindowsToolbox.Modules.InstalledApps.Utilities;

namespace WindowsToolbox.Modules.InstalledApps.Models;

public sealed class InstalledApplication : ObservableObject
{
    private long? _scannedSizeBytes;
    private DateTime? _scannedAt;
    private bool _isScanning;

    public string Id { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string DisplayVersion { get; init; } = string.Empty;
    public string Publisher { get; init; } = string.Empty;
    public DateTime? InstallDate { get; init; }
    public string InstallLocation { get; init; } = string.Empty;
    public string DisplayIconPath { get; init; } = string.Empty;
    public long? ReportedSizeBytes { get; init; }
    public string UninstallString { get; init; } = string.Empty;
    public string QuietUninstallString { get; init; } = string.Empty;
    public string ModifyPath { get; init; } = string.Empty;
    public bool IsSystemComponent { get; init; }
    public bool IsSystemEntry { get; init; }
    public bool CanUninstall { get; init; }
    public bool RequiresElevation { get; init; }
    public bool IsWindowsInstaller { get; init; }
    public bool NoRemove { get; init; }
    public string ReleaseType { get; init; } = string.Empty;
    public string ParentKeyName { get; init; } = string.Empty;
    public string Architecture { get; init; } = string.Empty;
    public string PackageId { get; set; } = string.Empty;
    public ApplicationSource Source { get; init; } = ApplicationSource.Registry;
    public string RegistryPath { get; init; } = string.Empty;

    public long? ScannedSizeBytes
    {
        get => _scannedSizeBytes;
        set
        {
            if (!SetProperty(ref _scannedSizeBytes, value))
                return;

            OnPropertyChanged(nameof(DisplaySizeBytes));
            OnPropertyChanged(nameof(SizeText));
            OnPropertyChanged(nameof(SizeSourceText));
            OnPropertyChanged(nameof(ScannedSizeText));
        }
    }

    public DateTime? ScannedAt
    {
        get => _scannedAt;
        set
        {
            if (SetProperty(ref _scannedAt, value))
                OnPropertyChanged(nameof(ScannedAtText));
        }
    }

    public bool IsScanning
    {
        get => _isScanning;
        set => SetProperty(ref _isScanning, value);
    }

    public long? DisplaySizeBytes => ScannedSizeBytes ?? ReportedSizeBytes;
    public string SizeText => SizeFormatter.Format(DisplaySizeBytes);
    public string ReportedSizeText => SizeFormatter.Format(ReportedSizeBytes);
    public string ScannedSizeText => SizeFormatter.Format(ScannedSizeBytes);
    public string SizeSourceText => ScannedSizeBytes.HasValue
        ? "目录扫描"
        : ReportedSizeBytes.HasValue ? "系统报告" : "未知";
    public string InstallDateText => InstallDate?.ToString("yyyy-MM-dd") ?? "未知";
    public string ScannedAtText => ScannedAt?.ToString("yyyy-MM-dd HH:mm") ?? "尚未扫描";
    public string SourceText => Source == ApplicationSource.Msix ? "Microsoft Store / MSIX" : "注册表";
    public string PublisherText => string.IsNullOrWhiteSpace(Publisher) ? "未知发布者" : Publisher;
    public string VersionText => string.IsNullOrWhiteSpace(DisplayVersion) ? "未知版本" : DisplayVersion;
    public string PackageIdText => string.IsNullOrWhiteSpace(PackageId) ? "未匹配" : PackageId;
    public string InstallLocationText => string.IsNullOrWhiteSpace(InstallLocation) ? "未知" : InstallLocation;
    public bool CanScan =>
        !string.IsNullOrWhiteSpace(InstallLocation) &&
        Directory.Exists(InstallLocation);
    public bool IsHighRisk =>
        IsSystemEntry ||
        IsSystemComponent ||
        Publisher.Contains("Microsoft", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("Security", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("安全", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("Runtime", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("运行库", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("Driver", StringComparison.OrdinalIgnoreCase) ||
        DisplayName.Contains("驱动", StringComparison.OrdinalIgnoreCase);
}
