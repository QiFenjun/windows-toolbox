using Microsoft.Win32;
using System.IO;
using System.Net;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

public interface ISystemProxyConfigurationProvider
{
    IReadOnlySet<int> GetConfiguredLoopbackPorts();
}

/// <summary>只读获取当前用户的 Windows 系统代理端口，不修改任何网络设置。</summary>
public sealed class SystemProxyConfigurationProvider : ISystemProxyConfigurationProvider
{
    private const string InternetSettingsKey = @"Software\Microsoft\Windows\CurrentVersion\Internet Settings";

    public IReadOnlySet<int> GetConfiguredLoopbackPorts()
    {
        HashSet<int> ports = [];
        if (!OperatingSystem.IsWindows())
            return ports;

        try
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(InternetSettingsKey, writable: false);
            if (key?.GetValue("ProxyEnable") is not int enabled || enabled == 0)
                return ports;

            string proxyServer = key?.GetValue("ProxyServer") as string ?? string.Empty;
            return ParseLoopbackPorts(proxyServer);
        }
        catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
        {
            return ports;
        }
    }

    public static IReadOnlySet<int> ParseLoopbackPorts(string proxyServer)
    {
        HashSet<int> ports = [];
        foreach (string entry in proxyServer.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string endpoint = entry.Contains('=') ? entry[(entry.IndexOf('=') + 1)..] : entry;
            if (!endpoint.Contains("://", StringComparison.Ordinal))
                endpoint = "http://" + endpoint;
            if (Uri.TryCreate(endpoint, UriKind.Absolute, out Uri? uri)
                && uri.Port is > 0 and <= ushort.MaxValue
                && IPAddress.TryParse(uri.Host, out IPAddress? address)
                && IPAddress.IsLoopback(address))
            {
                ports.Add(uri.Port);
            }
        }

        return ports;
    }
}
