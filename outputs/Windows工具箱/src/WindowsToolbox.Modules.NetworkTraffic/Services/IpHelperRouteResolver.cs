using System.Net;
using System.Runtime.InteropServices;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>用 IP Helper 的最佳路由接口补充外部 IPv4 连接路径；无结果即保持未知。</summary>
public interface IInterfaceRouteResolver
{
    bool TryResolve(string destinationAddress, IReadOnlyList<NetworkInterfaceSnapshot> interfaces, out NetworkInterfaceSnapshot networkInterface);
}

public sealed class IpHelperRouteResolver : IInterfaceRouteResolver
{
    public bool TryResolve(string destinationAddress, IReadOnlyList<NetworkInterfaceSnapshot> interfaces, out NetworkInterfaceSnapshot networkInterface)
    {
        networkInterface = null!;
        if (!IPAddress.TryParse(destinationAddress, out IPAddress? address) || address.AddressFamily != System.Net.Sockets.AddressFamily.InterNetwork)
            return false;
        byte[] bytes = address.GetAddressBytes();
        uint destination = BitConverter.ToUInt32(bytes, 0);
        if (GetBestInterface(destination, out uint index) != 0)
            return false;
        networkInterface = interfaces.FirstOrDefault(item => item.InterfaceIndex == index)!;
        return networkInterface is not null;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetBestInterface(uint destinationAddress, out uint bestInterfaceIndex);
}
