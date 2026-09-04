using System.Net;
using System.Runtime.InteropServices;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>通过 IP Helper 的扩展 TCP/UDP 表建立端口到 PID 的映射，不检查任何通信正文。</summary>
public sealed class IpHelperConnectionSnapshotProvider
{
    private const int AfInet = 2;
    private const int AfInet6 = 23;
    private const int TcpTableOwnerPidAll = 5;
    private const int UdpTableOwnerPid = 1;
    private const uint ErrorInsufficientBuffer = 122;

    public IReadOnlyList<NetworkConnectionSnapshot> Read()
    {
        List<NetworkConnectionSnapshot> connections = [];
        try
        {
            connections.AddRange(ReadTcp());
            connections.AddRange(ReadUdp());
            connections.AddRange(ReadTcp6());
            connections.AddRange(ReadUdp6());
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
        return connections;
    }

    private static IEnumerable<NetworkConnectionSnapshot> ReadTcp()
    {
        IntPtr buffer = GetTable((IntPtr pointer, ref int size) => GetExtendedTcpTable(
            pointer, ref size, true, AfInet, TcpTableOwnerPidAll, 0));
        try
        {
            int count = Marshal.ReadInt32(buffer);
            IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
            int rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            for (int index = 0; index < count; index++)
            {
                MibTcpRowOwnerPid row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPointer);
                yield return new NetworkConnectionSnapshot(
                    unchecked((int)row.OwningPid),
                    "TCP",
                    ToAddress(row.LocalAddress),
                    ToPort(row.LocalPort),
                    ToAddress(row.RemoteAddress),
                    ToPort(row.RemotePort),
                    TcpState(row.State),
                    row.State == 2);
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IEnumerable<NetworkConnectionSnapshot> ReadUdp()
    {
        IntPtr buffer = GetTable((IntPtr pointer, ref int size) => GetExtendedUdpTable(
            pointer, ref size, true, AfInet, UdpTableOwnerPid, 0));
        try
        {
            int count = Marshal.ReadInt32(buffer);
            IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
            int rowSize = Marshal.SizeOf<MibUdpRowOwnerPid>();
            for (int index = 0; index < count; index++)
            {
                MibUdpRowOwnerPid row = Marshal.PtrToStructure<MibUdpRowOwnerPid>(rowPointer);
                yield return new NetworkConnectionSnapshot(
                    unchecked((int)row.OwningPid),
                    "UDP",
                    ToAddress(row.LocalAddress),
                    ToPort(row.LocalPort),
                    "*",
                    0,
                    "Bound",
                    true);
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IEnumerable<NetworkConnectionSnapshot> ReadTcp6()
    {
        IntPtr buffer = GetTable((IntPtr pointer, ref int size) => GetExtendedTcpTable(
            pointer, ref size, true, AfInet6, TcpTableOwnerPidAll, 0));
        try
        {
            int count = Marshal.ReadInt32(buffer);
            IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
            int rowSize = Marshal.SizeOf<MibTcp6RowOwnerPid>();
            for (int index = 0; index < count; index++)
            {
                MibTcp6RowOwnerPid row = Marshal.PtrToStructure<MibTcp6RowOwnerPid>(rowPointer);
                yield return new NetworkConnectionSnapshot(
                    unchecked((int)row.OwningPid), "TCP", ToAddress(row.LocalAddress), ToPort(row.LocalPort),
                    ToAddress(row.RemoteAddress), ToPort(row.RemotePort), TcpState(row.State), row.State == 2);
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IEnumerable<NetworkConnectionSnapshot> ReadUdp6()
    {
        IntPtr buffer = GetTable((IntPtr pointer, ref int size) => GetExtendedUdpTable(
            pointer, ref size, true, AfInet6, UdpTableOwnerPid, 0));
        try
        {
            int count = Marshal.ReadInt32(buffer);
            IntPtr rowPointer = IntPtr.Add(buffer, sizeof(int));
            int rowSize = Marshal.SizeOf<MibUdp6RowOwnerPid>();
            for (int index = 0; index < count; index++)
            {
                MibUdp6RowOwnerPid row = Marshal.PtrToStructure<MibUdp6RowOwnerPid>(rowPointer);
                yield return new NetworkConnectionSnapshot(
                    unchecked((int)row.OwningPid), "UDP", ToAddress(row.LocalAddress), ToPort(row.LocalPort), "*", 0, "Bound", true);
                rowPointer = IntPtr.Add(rowPointer, rowSize);
            }
        }
        finally { Marshal.FreeHGlobal(buffer); }
    }

    private static IntPtr GetTable(TableReader reader)
    {
        int size = 0;
        uint result = reader(IntPtr.Zero, ref size);
        if (result != ErrorInsufficientBuffer || size <= 0)
            throw new InvalidOperationException("IP Helper 表不可用。");
        IntPtr buffer = Marshal.AllocHGlobal(size);
        result = reader(buffer, ref size);
        if (result != 0)
        {
            Marshal.FreeHGlobal(buffer);
            throw new InvalidOperationException("读取 IP Helper 表失败。");
        }
        return buffer;
    }

    private static string ToAddress(uint address) => new IPAddress(address).ToString();
    private static string ToAddress(byte[] address) => new IPAddress(address).ToString();
    private static int ToPort(byte[] bytes) => bytes.Length >= 2 ? (bytes[0] << 8) | bytes[1] : 0;
    private static string TcpState(uint state) => state switch
    {
        2 => "Listen", 5 => "Established", 8 => "CloseWait", 11 => "TimeWait", _ => state.ToString()
    };

    private delegate uint TableReader(IntPtr table, ref int size);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable, ref int size, bool sort, int ipVersion, int tableClass, uint reserved);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedUdpTable(
        IntPtr udpTable, ref int size, bool sort, int ipVersion, int tableClass, uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] LocalPort;
        public uint RemoteAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdpRowOwnerPid
    {
        public uint LocalAddress;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] LocalPort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcp6RowOwnerPid
    {
        public uint State;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddress;
        public uint LocalScopeId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] LocalPort;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] RemoteAddress;
        public uint RemoteScopeId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] RemotePort;
        public uint OwningPid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MibUdp6RowOwnerPid
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)] public byte[] LocalAddress;
        public uint LocalScopeId;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)] public byte[] LocalPort;
        public uint OwningPid;
    }
}
