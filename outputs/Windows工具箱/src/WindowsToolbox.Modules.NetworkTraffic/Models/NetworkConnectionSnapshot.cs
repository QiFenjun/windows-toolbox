namespace WindowsToolbox.Modules.NetworkTraffic.Models;

public sealed record NetworkConnectionSnapshot(
    int ProcessId,
    string Protocol,
    string LocalAddress,
    int LocalPort,
    string RemoteAddress,
    int RemotePort,
    string State,
    bool IsListener = false);
