namespace WindowsToolbox.Modules.NetworkTraffic.Models;

/// <summary>ETW 仅提供的网络元数据；不保存数据包、URL 或通信正文。</summary>
public sealed record NetworkTrafficEvent(
    DateTimeOffset Timestamp,
    int ProcessId,
    string Protocol,
    TrafficDirection Direction,
    long SizeBytes,
    string SourceAddress,
    int SourcePort,
    string DestinationAddress,
    int DestinationPort);
