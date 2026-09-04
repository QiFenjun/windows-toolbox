using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Session;
using System.Net;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>在提升后的辅助模式中读取 Kernel TCP/IP ETW 元数据；不抓包也不解析负载。</summary>
public sealed class EtwTrafficEventSource
{
    public async Task RunAsync(Action<NetworkTrafficEvent> onEvent, CancellationToken cancellationToken)
    {
        string sessionName = $"WindowsToolbox.NetworkTraffic.{Environment.ProcessId}.{Guid.NewGuid():N}";
        using TraceEventSession session = new(sessionName);
        session.StopOnDispose = true;
        session.EnableKernelProvider(KernelTraceEventParser.Keywords.NetworkTCPIP);
        session.Source.Dynamic.All += traceEvent => TryPublish(traceEvent, onEvent);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => session.Stop());
        await Task.Run(() => session.Source.Process(), CancellationToken.None).ConfigureAwait(false);
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

        onEvent(new NetworkTrafficEvent(
            traceEvent.TimeStamp,
            traceEvent.ProcessID,
            tcp ? "TCP" : "UDP",
            direction.Value,
            size,
            ToAddress(GetPayload(traceEvent, "saddr", "SourceAddress", "srcaddr")),
            (int)ToInt64(GetPayload(traceEvent, "sport", "SourcePort", "srcport")),
            ToAddress(GetPayload(traceEvent, "daddr", "DestinationAddress", "dstaddr")),
            (int)ToInt64(GetPayload(traceEvent, "dport", "DestinationPort", "dstport"))));
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
