namespace WindowsToolbox.Modules.InstalledApps.Models;

public sealed record ApplicationSizeInfo(
    long SizeBytes,
    DateTime ScannedAt,
    long FileCount,
    int SkippedEntryCount);

public sealed record SizeScanProgress(long SizeBytes, long FileCount);
