using System.Runtime.InteropServices;
using System.Text;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>只读枚举当前调用方有权限查看的 ETW 会话；绝不停止或修改未知会话。</summary>
public interface IEtwSessionDiagnostics
{
    EtwSessionDiagnosticsSnapshot Capture();
}

public sealed class EtwSessionDiagnostics : IEtwSessionDiagnostics
{
    private const uint ErrorMoreData = 234;
    private const uint ErrorSuccess = 0;
    private const uint EventTraceSystemLoggerMode = 0x02000000;
    private const int SessionNameCharacters = 1024;
    private const int LogFileNameCharacters = 1024;

    public EtwSessionDiagnosticsSnapshot Capture()
    {
        if (!OperatingSystem.IsWindows())
            return new([], 0, "当前平台不支持 ETW Session 诊断。");

        try
        {
            uint capacity = 64;
            for (int attempt = 0; attempt < 4; attempt++)
            {
                List<IntPtr> allocations = [];
                try
                {
                    int propertiesSize = Marshal.SizeOf<EventTraceProperties>();
                    int recordSize = propertiesSize + (SessionNameCharacters + LogFileNameCharacters) * sizeof(char);
                    IntPtr[] propertyArray = new IntPtr[capacity];
                    for (int index = 0; index < propertyArray.Length; index++)
                    {
                        IntPtr pointer = Marshal.AllocHGlobal(recordSize);
                        allocations.Add(pointer);
                        Marshal.Copy(new byte[recordSize], 0, pointer, recordSize);
                        EventTraceProperties properties = new()
                        {
                            Wnode = new WnodeHeader { BufferSize = (uint)recordSize, Flags = 0x00020000 },
                            LoggerNameOffset = (uint)propertiesSize,
                            LogFileNameOffset = (uint)(propertiesSize + SessionNameCharacters * sizeof(char))
                        };
                        Marshal.StructureToPtr(properties, pointer, false);
                        propertyArray[index] = pointer;
                    }

                    uint status = QueryAllTraces(propertyArray, capacity, out uint loggerCount);
                    if (status == ErrorMoreData)
                    {
                        capacity = Math.Min(Math.Max(capacity * 2, loggerCount + 16), 1024);
                        continue;
                    }
                    if (status != ErrorSuccess)
                        return new([], status, $"QueryAllTraces 返回 Win32 错误 {status}。");

                    List<EtwSessionInfo> sessions = [];
                    for (int index = 0; index < loggerCount && index < propertyArray.Length; index++)
                    {
                        EventTraceProperties properties = Marshal.PtrToStructure<EventTraceProperties>(propertyArray[index]);
                        string name = ReadString(propertyArray[index], properties.LoggerNameOffset, recordSize);
                        if (string.IsNullOrWhiteSpace(name))
                            name = "(未命名会话)";
                        sessions.Add(new(
                            name,
                            properties.LogFileMode,
                            properties.BufferSize,
                            properties.MinimumBuffers,
                            properties.MaximumBuffers,
                            properties.FlushTimer,
                            properties.Wnode.HistoricalContext,
                            (properties.LogFileMode & EventTraceSystemLoggerMode) != 0));
                    }
                    return new(sessions, ErrorSuccess, null);
                }
                finally
                {
                    foreach (IntPtr allocation in allocations)
                        Marshal.FreeHGlobal(allocation);
                }
            }

            return new([], ErrorMoreData, "ETW 会话数量超过安全诊断上限。");
        }
        catch (Exception exception) when (exception is ExternalException or OutOfMemoryException)
        {
            return new([], unchecked((uint)exception.HResult), "无法读取 ETW 会话诊断信息。");
        }
    }

    private static string ReadString(IntPtr basePointer, uint offset, int recordSize)
    {
        if (offset >= recordSize)
            return string.Empty;

        string value = Marshal.PtrToStringUni(IntPtr.Add(basePointer, (int)offset)) ?? string.Empty;
        return ReplaceInvalidSurrogates(value);
    }

    private static string ReplaceInvalidSurrogates(string value)
    {
        StringBuilder? sanitized = null;
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];
            if (!char.IsSurrogate(current))
            {
                sanitized?.Append(current);
                continue;
            }

            if (char.IsHighSurrogate(current)
                && index + 1 < value.Length
                && char.IsLowSurrogate(value[index + 1]))
            {
                if (sanitized is not null)
                {
                    sanitized.Append(current);
                    sanitized.Append(value[++index]);
                }
                else
                {
                    index++;
                }
                continue;
            }

            sanitized ??= new StringBuilder(value.Length).Append(value, 0, index);
            sanitized.Append('\uFFFD');
        }

        return sanitized?.ToString() ?? value;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "QueryAllTracesW")]
    private static extern uint QueryAllTraces(IntPtr[] propertyArray, uint propertyArrayCount, out uint loggerCount);

    [StructLayout(LayoutKind.Sequential)]
    private struct WnodeHeader
    {
        public uint BufferSize;
        public uint ProviderId;
        public ulong HistoricalContext;
        public ulong KernelHandleOrTimeStamp;
        public Guid Guid;
        public uint ClientContext;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EventTraceProperties
    {
        public WnodeHeader Wnode;
        public uint BufferSize;
        public uint MinimumBuffers;
        public uint MaximumBuffers;
        public uint MaximumFileSize;
        public uint LogFileMode;
        public uint FlushTimer;
        public uint EnableFlags;
        public int AgeLimit;
        public uint NumberOfBuffers;
        public uint FreeBuffers;
        public uint EventsLost;
        public uint BuffersWritten;
        public uint LogBuffersLost;
        public uint RealTimeBuffersLost;
        public IntPtr LoggerThreadId;
        public uint LogFileNameOffset;
        public uint LoggerNameOffset;
    }
}

public sealed record EtwSessionInfo(
    string SessionName,
    uint LogFileMode,
    uint BufferSizeKb,
    uint MinimumBuffers,
    uint MaximumBuffers,
    uint FlushTimerSeconds,
    ulong HistoricalContext,
    bool IsSystemLogger);

public sealed record EtwSessionDiagnosticsSnapshot(
    IReadOnlyList<EtwSessionInfo> Sessions,
    uint NativeStatus,
    string? ErrorMessage)
{
    public int VisibleSessionCount => Sessions
        .DistinctBy(session => (session.SessionName, session.HistoricalContext))
        .Count();
    public int SystemLoggerCount => Sessions
        .Where(session => session.IsSystemLogger)
        .DistinctBy(session => (session.SessionName, session.HistoricalContext))
        .Count();
    public int WindowsToolboxSessionCount => Sessions
        .Where(session => EtwSessionNames.IsWindowsToolboxSession(session.SessionName))
        .Select(session => session.SessionName)
        .Distinct(StringComparer.Ordinal)
        .Count();
}
