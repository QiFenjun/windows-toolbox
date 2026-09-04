using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.Net;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>在提升后的辅助模式中读取 Kernel TCP/IP ETW 元数据；不抓包也不解析负载。</summary>
public sealed class EtwTrafficEventSource
{
    public async Task RunAsync(
        Action<NetworkTrafficEvent> onEvent,
        CancellationToken cancellationToken,
        Action? onStarted = null)
    {
        TraceEventSession session;
        try { session = new TraceEventSession(EtwSessionNames.NetworkTraffic); }
        catch (Exception exception) { throw new EtwSessionStartupException("创建 ETW 会话", exception); }

        using (session)
        {
            try
            {
                // 默认缓冲预留对轻量实时元数据监控过大，资源紧张时会导致 StartTrace 返回 0x800705AA。
                // 本模块不记录包内容或 ETL 文件，4 MB 足以平滑短时网络事件峰值。
                session.BufferSizeMB = 4;
                session.StopOnDispose = true;
            }
            catch (Exception exception) { throw new EtwSessionStartupException("配置 ETW 会话", exception); }

            try { session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP); }
            catch (Exception exception) { throw new EtwSessionStartupException("启用内核网络 Provider", exception); }

            try
            {
                session.Source.Dynamic.All += traceEvent => TryPublish(traceEvent, onEvent);
                using CancellationTokenRegistration registration = cancellationToken.Register(() => session.Stop());
                onStarted?.Invoke();
                await Task.Run(() => session.Source.Process(), CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                throw new EtwSessionStartupException("打开实时 ETW 事件源", exception);
            }
        }
    }

    /// <summary>仅停止使用固定专属名称的会话；绝不触及其他 ETW Session。</summary>
    public static bool StopOwnedSessionIfPresent()
    {
        using TraceEventSession? session = TraceEventSession.GetActiveSession(EtwSessionNames.NetworkTraffic);
        if (session is null)
            return false;
        session.Stop();
        return true;
    }

    /// <summary>只回收严格匹配旧命名格式且其所属进程已经退出的本软件会话。</summary>
    public static EtwSessionCleanupResult StopLegacyOrphanSessions(
        IEnumerable<EtwSessionInfo> sessions,
        Func<int, bool>? isProcessRunning = null)
    {
        isProcessRunning ??= EtwSessionNames.IsProcessRunning;
        List<string> stopped = [];
        List<string> failed = [];

        foreach (string sessionName in sessions
            .Select(session => session.SessionName)
            .Where(name => EtwSessionNames.IsLegacyOrphanCandidate(name, isProcessRunning))
            .Distinct(StringComparer.Ordinal))
        {
            try
            {
                using TraceEventSession? session = TraceEventSession.GetActiveSession(sessionName);
                if (session is null)
                    continue;
                session.Stop();
                stopped.Add(sessionName);
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                failed.Add($"{sessionName}:0x{unchecked((uint)exception.HResult):X8}");
            }
        }

        return new(stopped, failed);
    }

    internal static void TryPublish(TraceEvent traceEvent, Action<NetworkTrafficEvent> onEvent)
    {
        string name = traceEvent.EventName ?? string.Empty;
        TrafficDirection? direction = name.Contains("Send", StringComparison.OrdinalIgnoreCase)
            ? TrafficDirection.Upload
            : name.Contains("Recv", StringComparison.OrdinalIgnoreCase) || name.Contains("Receive", StringComparison.OrdinalIgnoreCase)
                ? TrafficDirection.Download
                : null;
        bool tcp = name.Contains("Tcp", StringComparison.OrdinalIgnoreCase);
        bool udp = name.Contains("Udp", StringComparison.OrdinalIgnoreCase);
        if (direction is null || (!tcp && !udp))
            return;

        long size = ToInt64(GetPayload(traceEvent, "size", "Size", "dsize", "DataSize"));
        if (size <= 0)
            return;

        // Kernel Network ETW 的事件头 PID 在部分动态事件中为 -1；实际归属 PID 位于负载字段。
        int processId = ResolveProcessId(
            traceEvent.ProcessID,
            GetPayload(traceEvent, "PID", "ProcessId", "ProcessID", "processId", "Process_Id"));
        if (processId <= 0)
            return;

        onEvent(new NetworkTrafficEvent(
            traceEvent.TimeStamp,
            processId,
            tcp ? "TCP" : "UDP",
            direction.Value,
            size,
            ToAddress(GetPayload(traceEvent, "saddr", "SourceAddress", "srcaddr")),
            (int)ToInt64(GetPayload(traceEvent, "sport", "SourcePort", "srcport")),
            ToAddress(GetPayload(traceEvent, "daddr", "DestinationAddress", "dstaddr")),
            (int)ToInt64(GetPayload(traceEvent, "dport", "DestinationPort", "dstport"))));
    }

    /// <summary>优先使用 ETW 负载中的真实 PID，事件头仅作为兼容旧事件的后备。</summary>
    public static int ResolveProcessId(int eventHeaderProcessId, object? payloadProcessId)
    {
        long payloadId = ToInt64(payloadProcessId);
        if (payloadId is > 0 and <= int.MaxValue)
            return (int)payloadId;

        return eventHeaderProcessId > 0 ? eventHeaderProcessId : 0;
    }

    private static object? GetPayload(TraceEvent traceEvent, params string[] names)
    {
        foreach (string wanted in names)
        {
            for (int index = 0; index < traceEvent.PayloadNames.Length; index++)
            {
                if (string.Equals(traceEvent.PayloadNames[index], wanted, StringComparison.OrdinalIgnoreCase))
                    return traceEvent.PayloadValue(index);
            }
        }
        return null;
    }

    private static long ToInt64(object? value)
    {
        if (value is null)
            return 0;
        if (value is IConvertible)
        {
            try { return Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture); }
            catch (FormatException) { }
            catch (OverflowException) { }
        }
        if (long.TryParse(value.ToString(), out long parsed))
            return parsed;
        return 0;
    }

    private static string ToAddress(object? value)
    {
        if (value is null)
            return string.Empty;
        string text = value.ToString() ?? string.Empty;
        if (IPAddress.TryParse(text, out IPAddress? parsed))
            return parsed.ToString();
        if (value is IConvertible)
        {
            try { return new IPAddress(Convert.ToUInt32(value, System.Globalization.CultureInfo.InvariantCulture)).ToString(); }
            catch (FormatException) { }
            catch (OverflowException) { }
        }
        return text;
    }
}

public sealed record EtwSessionCleanupResult(
    IReadOnlyList<string> StoppedSessions,
    IReadOnlyList<string> FailedSessions);

/// <summary>保留 ETW 生命周期的失败阶段，便于提供可操作的本机诊断。</summary>
public sealed class EtwSessionStartupException(string stage, Exception innerException)
    : Exception($"{stage}失败：{innerException.Message}", innerException)
{
    public string Stage { get; } = stage;
}
