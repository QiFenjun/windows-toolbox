namespace WindowsToolbox.Modules.NetworkTraffic.Models;

public sealed record NetworkInterfaceSnapshot(
    string Id,
    int InterfaceIndex,
    string Name,
    string Description,
    string Type,
    NetworkPathKind PathKind,
    bool IsOperational,
    long ReceivedBytes,
    long SentBytes,
    double DownloadBytesPerSecond,
    double UploadBytesPerSecond);
