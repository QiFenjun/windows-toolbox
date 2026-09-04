using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Channels;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>由同一 EXE 的 runas 辅助模式承载 ETW，会话仅在该进程内提升。</summary>
public static class NetworkMonitorHelperHost
{
    private const string HelperMutexName = "Global\\WindowsToolbox.NetworkTraffic.Helper";

    public static async Task<int> RunAsync(string pipeName)
    {
        try
        {
            using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(15000).ConfigureAwait(false);
            await using StreamWriter writer = new(pipe, leaveOpen: true) { AutoFlush = true };
            using StreamReader commandReader = new(pipe, leaveOpen: true);
            using NamedMutexLease? helperMutex = await NamedMutexLease.TryAcquireAsync(HelperMutexName, CancellationToken.None)
                .ConfigureAwait(false);
            if (helperMutex is null)
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(new NetworkHelperMessage(
                    "error", "高级网络监控已由另一个 Windows工具箱实例运行。"))).ConfigureAwait(false);
                return 4;
            }

            using CancellationTokenSource cancellation = new();
            Task commandTask = WatchCommandsAsync(commandReader, cancellation);
            Channel<NetworkHelperMessage> messages = Channel.CreateBounded<NetworkHelperMessage>(
                new BoundedChannelOptions(1024)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                });

            Task writerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (NetworkHelperMessage message in messages.Reader.ReadAllAsync().ConfigureAwait(false))
                    {
                        if (cancellation.IsCancellationRequested && message.Kind == "traffic")
                            continue;
                        await writer.WriteLineAsync(JsonSerializer.Serialize(message)).ConfigureAwait(false);
                    }
                }
                catch (IOException) { cancellation.Cancel(); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            });
            bool etwStarted = false;

            try
            {
                EtwSessionDiagnosticsSnapshot diagnostics = new EtwSessionDiagnostics().Capture();
                WriteSessionDiagnostics(diagnostics, "LegacyKernel", "BeforeStart");

                // 固定名会话在全局 Helper 互斥锁已独占的前提下只能是本程序上次异常退出的遗留。
                bool fixedSessionStopped = EtwTrafficEventSource.StopOwnedSessionIfPresent();
                EtwSessionCleanupResult cleanup = EtwTrafficEventSource.StopLegacyOrphanSessions(diagnostics.Sessions);
                WriteCleanupDiagnostic(cleanup, fixedSessionStopped);
                if (fixedSessionStopped || cleanup.StoppedSessions.Count > 0)
                {
                    diagnostics = new EtwSessionDiagnostics().Capture();
                    WriteSessionDiagnostics(diagnostics, "LegacyKernel", "AfterOwnedCleanup");
                }

                await messages.Writer.WriteAsync(new NetworkHelperMessage("status", "高级实时流量监控正在启动。"), cancellation.Token)
                    .ConfigureAwait(false);
                EtwTrafficEventSource source = new();
                await source.RunAsync(trafficEvent =>
                {
                    // ETW 回调不等待管道写入；高流量时仅丢弃过载事件，避免阻塞内核事件线程。
                    messages.Writer.TryWrite(new NetworkHelperMessage("traffic", TrafficEvent: trafficEvent));
                }, cancellation.Token, () =>
                {
                    etwStarted = true;
                    messages.Writer.TryWrite(new NetworkHelperMessage(
                        "status", "高级实时流量监控已启动。应用统计与接口统计分别显示，不会相加。"));
                }).ConfigureAwait(false);
            }
            catch (EtwSessionStartupException exception)
            {
                WriteStartupDiagnostic(exception.Stage, exception);
                await messages.Writer.WriteAsync(new NetworkHelperMessage(
                    "error",
                    $"高级 ETW 监控在“{exception.Stage}”阶段失败（诊断代码 ETW-{unchecked((uint)(exception.InnerException?.HResult ?? 0)):X8}）。"), CancellationToken.None).ConfigureAwait(false);
                return 2;
            }
            catch (UnauthorizedAccessException)
            {
                WriteStartupDiagnostic("UnauthorizedAccess", null);
                await messages.Writer.WriteAsync(new NetworkHelperMessage(
                    "error",
                    "无法创建高级 ETW 监控会话。请确认已在 UAC 提示中允许管理员权限。"), CancellationToken.None).ConfigureAwait(false);
                return 3;
            }
            catch (InvalidOperationException)
            {
                WriteStartupDiagnostic("InvalidOperation", null);
                await messages.Writer.WriteAsync(new NetworkHelperMessage(
                    "error",
                    "Windows ETW 会话无法启动。请关闭其他使用内核网络跟踪的监控工具后重试。"), CancellationToken.None).ConfigureAwait(false);
                return 2;
            }
            catch (Exception exception)
            {
                WriteStartupDiagnostic("Unexpected", exception);
                await messages.Writer.WriteAsync(new NetworkHelperMessage(
                    "error",
                    $"高级 ETW 监控启动失败（诊断代码 ETW-{unchecked((uint)exception.HResult):X8}），请重新启动监控；若问题持续，请重启 Windows 后再试。"), CancellationToken.None).ConfigureAwait(false);
                return 2;
            }
            finally
            {
                Stopwatch gracefulStopwatch = Stopwatch.StartNew();
                cancellation.Cancel();
                messages.Writer.TryWrite(new NetworkHelperMessage("stopped", "高级网络监控已正常停止。"));
                messages.Writer.TryComplete();
                await writerTask.ConfigureAwait(false);
                try { await commandTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
                gracefulStopwatch.Stop();
                if (etwStarted)
                    WriteLifecycleDiagnostic(gracefulStopwatch.Elapsed, gracefulShutdown: true, forcedKill: false);
            }

            return 0;
        }
        catch (UnauthorizedAccessException)
        {
            WriteStartupDiagnostic("OuterUnauthorizedAccess", null);
            return 3;
        }
        catch (IOException)
        {
            // 主程序已关闭管道时，Helper 无需把流量管道断开记作启动故障。
            return 0;
        }
        catch (Exception exception)
        {
            WriteStartupDiagnostic("OuterUnexpected", exception);
            return 2;
        }
    }

    private static void WriteStartupDiagnostic(string category, Exception? exception)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsToolbox", "NetworkTraffic");
            Directory.CreateDirectory(directory);
            string details = exception is null
                ? category
                : $"{category}|{exception.GetType().FullName}|0x{unchecked((uint)exception.HResult):X8}|{exception.Message.ReplaceLineEndings(" ")}";
            File.AppendAllText(Path.Combine(directory, "helper-diagnostics.log"),
                $"{DateTimeOffset.UtcNow:O}|{details}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteSessionDiagnostics(EtwSessionDiagnosticsSnapshot snapshot, string backend, string stage)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsToolbox", "NetworkTraffic");
            Directory.CreateDirectory(directory);
            string sessions = string.Join(",", snapshot.Sessions
                .Where(session => EtwSessionNames.IsWindowsToolboxSession(session.SessionName))
                .Select(session => session.SessionName));
            string logPath = Path.Combine(directory, "helper-diagnostics.log");
            File.AppendAllText(logPath,
                $"{DateTimeOffset.UtcNow:O}|Backend={backend}|Stage={stage}|VisibleSessions={snapshot.VisibleSessionCount}|SystemLoggers={snapshot.SystemLoggerCount}|WindowsToolboxSessions={snapshot.WindowsToolboxSessionCount}|Status={snapshot.NativeStatus}|OwnedSessions={sessions}{Environment.NewLine}");
            foreach (EtwSessionInfo session in snapshot.Sessions)
            {
                File.AppendAllText(logPath,
                    $"{DateTimeOffset.UtcNow:O}|SessionDetail|Name={session.SessionName}|Mode=0x{session.LogFileMode:X8}|BufferSizeKb={session.BufferSizeKb}|MinimumBuffers={session.MinimumBuffers}|MaximumBuffers={session.MaximumBuffers}|FlushTimerSeconds={session.FlushTimerSeconds}|Handle=0x{session.HistoricalContext:X}|IsSystemLogger={session.IsSystemLogger}|OwnedByWindowsToolbox={EtwSessionNames.IsWindowsToolboxSession(session.SessionName)}{Environment.NewLine}");
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteCleanupDiagnostic(EtwSessionCleanupResult result, bool fixedSessionStopped)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsToolbox", "NetworkTraffic");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "helper-diagnostics.log"),
                $"{DateTimeOffset.UtcNow:O}|OwnedCleanup|FixedStopped={fixedSessionStopped}|LegacyStopped={string.Join(',', result.StoppedSessions)}|Failed={string.Join(',', result.FailedSessions)}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static void WriteLifecycleDiagnostic(TimeSpan duration, bool gracefulShutdown, bool forcedKill)
    {
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WindowsToolbox", "NetworkTraffic");
            Directory.CreateDirectory(directory);
            File.AppendAllText(Path.Combine(directory, "helper-diagnostics.log"),
                $"{DateTimeOffset.UtcNow:O}|Lifecycle|GracefulShutdown={gracefulShutdown}|GracefulStopDurationMs={duration.TotalMilliseconds:F0}|ForcedKill={forcedKill}{Environment.NewLine}");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static async Task WatchCommandsAsync(StreamReader reader, CancellationTokenSource cancellation)
    {
        try
        {
            while (!cancellation.IsCancellationRequested)
            {
                string? line = await reader.ReadLineAsync(cancellation.Token).ConfigureAwait(false);
                if (line is null)
                {
                    cancellation.Cancel();
                    return;
                }

                NetworkHelperMessage? message = JsonSerializer.Deserialize<NetworkHelperMessage>(line);
                if (string.Equals(message?.Kind, "command", StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(message?.Command, "stop", StringComparison.OrdinalIgnoreCase))
                {
                    cancellation.Cancel();
                    return;
                }
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        catch (IOException) { cancellation.Cancel(); }
        catch (JsonException) { }
    }
}
