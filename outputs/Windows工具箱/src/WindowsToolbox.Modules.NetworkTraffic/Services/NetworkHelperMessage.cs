using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>命名管道消息只包含状态、错误说明或统计元数据。</summary>
public sealed record NetworkHelperMessage(
    string Kind,
    string Message = "",
    NetworkTrafficEvent? TrafficEvent = null,
    string? Command = null);
