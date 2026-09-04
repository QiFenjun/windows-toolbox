using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using System.Threading.Channels;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>由同一 EXE 的 runas 辅助模式承载 ETW，会话仅在该进程内提升。</summary>
public static class NetworkMonitorHelperHost
{
    public static async Task<int> RunAsync(string pipeName)
    {
        try
        {
            using NamedPipeClientStream pipe = new(".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);
            await pipe.ConnectAsync(15000).ConfigureAwait(false);
            await using StreamWriter writer = new(pipe) { AutoFlush = true };
            using CancellationTokenSource cancellation = new();
            Channel<NetworkHelperMessage> messages = Channel.CreateBounded<NetworkHelperMessage>(
                new BoundedChannelOptions(8192)
                {
                    FullMode = BoundedChannelFullMode.DropWrite,
                    SingleReader = true,
                    SingleWriter = false
                });

            Task writerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (NetworkHelperMessage message in messages.Reader.ReadAllAsync(cancellation.Token).ConfigureAwait(false))
                    {
                        await writer.WriteLineAsync(JsonSerializer.Serialize(message)).ConfigureAwait(false);
                    }
                }
                catch (IOException) { cancellation.Cancel(); }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            });

            await messages.Writer.WriteAsync(new NetworkHelperMessage("status", "高级实时流量监控已启动。"), cancellation.Token)
                .ConfigureAwait(false);
            EtwTrafficEventSource source = new();
            try
            {
                await source.RunAsync(trafficEvent =>
                {
                    // ETW 回调不等待管道写入；高流量时仅丢弃过载事件，避免阻塞内核事件线程。
                    messages.Writer.TryWrite(new NetworkHelperMessage("traffic", TrafficEvent: trafficEvent));
                }, cancellation.Token).ConfigureAwait(false);
            }
            finally
            {
                messages.Writer.TryComplete();
                await writerTask.ConfigureAwait(false);
            }

            return 0;
        }
        catch (UnauthorizedAccessException) { return 3; }
        catch (Exception) { return 2; }
    }
}
