using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

public sealed class ProcessTrafficResolver
{
    public TrafficProcessSnapshot Resolve(
        TrafficProcessIdentity identity,
        long uploadBytes,
        long downloadBytes,
        double uploadRate,
        double downloadRate,
        IReadOnlyList<NetworkConnectionSnapshot> connections,
        NetworkPathResolution path)
    {
        string name = identity.ProcessId is 0 or 4 ? "System / PID " + identity.ProcessId : "PID " + identity.ProcessId;
        string pathName = string.Empty;
        string productName = string.Empty;
        string companyName = string.Empty;
        bool identityResolved = false;

        try
        {
            using Process process = Process.GetProcessById(identity.ProcessId);
            name = process.ProcessName;
            identityResolved = true;
            try { pathName = process.MainModule?.FileName ?? string.Empty; } catch (Win32Exception) { }
            if (!string.IsNullOrWhiteSpace(pathName) && File.Exists(pathName))
            {
                FileVersionInfo version = FileVersionInfo.GetVersionInfo(pathName);
                productName = version.ProductName ?? string.Empty;
                companyName = version.CompanyName ?? string.Empty;
            }
        }
        catch (ArgumentException) { }
        catch (InvalidOperationException) { }
        catch (Win32Exception) { }

        return new TrafficProcessSnapshot(
            identity,
            name,
            pathName,
            productName,
            companyName,
            uploadRate,
            downloadRate,
            uploadBytes,
            downloadBytes,
            connections,
            path.Kind,
            path.InterfaceName,
            path.Kind == NetworkPathKind.Vpn,
            path.Kind == NetworkPathKind.LocalProxy,
            path.Kind == NetworkPathKind.Loopback,
            path.IsAttributionUncertain,
            identityResolved ? TrafficIdentityStatus.Resolved : TrafficIdentityStatus.SyntheticProcessId);
    }
}
