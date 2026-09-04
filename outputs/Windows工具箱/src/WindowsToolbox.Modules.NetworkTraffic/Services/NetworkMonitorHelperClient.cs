using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text.Json;
using WindowsToolbox.Modules.NetworkTraffic.Models;

namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>主程序普通权限运行；此客户端只在用户请求时启动 runas 辅助模式。</summary>
public sealed class NetworkMonitorHelperClient : IAsyncDisposable
{
    private NamedPipeServerStream? _pipe;
    private Process? _helperProcess;
    private CancellationTokenSource? _cancellation;
    private Task? _readerTask;

    public event EventHandler<NetworkTrafficEvent>? TrafficReceived;
    public event EventHandler<string>? StatusChanged;
    public event EventHandler? Stopped;

    public bool IsRunning => _helperProcess is { HasExited: false } && _pipe?.IsConnected == true;

    public async Task<bool> StartAsync(CancellationToken cancellationToken)
    {
        if (IsRunning)
            return true;
        await StopAsync().ConfigureAwait(false);
        _cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        string pipeName = $"WindowsToolbox.NetworkTraffic.{Environment.ProcessId}.{Guid.NewGuid():N}";
        _pipe = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        try
        {
            string executable = Environment.ProcessPath ?? throw new InvalidOperationException("无法找到主程序路径。");
            ProcessStartInfo startInfo = new(executable, $"--network-monitor-helper \"{pipeName}\"")
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            _helperProcess = Process.Start(startInfo) ?? throw new InvalidOperationException("无法启动网络监控辅助进程。");
        }
        catch (Win32Exception exception) when (exception.NativeErrorCode == 1223)
        {
            StatusChanged?.Invoke(this, "高级实时流量监控需要管理员权限。其他功能仍可正常使用。");
            await StopAsync().ConfigureAwait(false);
            return false;
        }
        catch (Exception)
        {
            StatusChanged?.Invoke(this, "无法启动网络监控辅助进程，请稍后重试。");
            await StopAsync().ConfigureAwait(false);
            return false;
        }

        try
        {
            await _pipe.WaitForConnectionAsync(_cancellation.Token).ConfigureAwait(false);
            _readerTask = ReadAsync(_pipe, _cancellation.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke(this, "网络监控启动已取消。");
            await StopAsync().ConfigureAwait(false);
            return false;
        }
        catch (IOException)
        {
            StatusChanged?.Invoke(this, "网络监控辅助进程未能连接。");
            await StopAsync().ConfigureAwait(false);
            return false;
        }
    }

    public async Task StopAsync()
    {
        _cancellation?.Cancel();
        if (_readerTask is not null)
        {
            try { await _readerTask.ConfigureAwait(false); }
            catch (OperationCanceledException) { }
            catch (IOException) { }
        }
        _pipe?.Dispose();
        _pipe = null;
        _cancellation?.Dispose();
        _cancellation = null;
        if (_helperProcess is { HasExited: false })
        {
            try { _helperProcess.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
        _helperProcess?.Dispose();
        _helperProcess = null;
        _readerTask = null;
    }

    public async ValueTask DisposeAsync() => await StopAsync().ConfigureAwait(false);

    private async Task ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        using StreamReader reader = new(stream);
        while (!cancellationToken.IsCancellationRequested)
        {
            string? line = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (line is null)
                break;
            NetworkHelperMessage? message = JsonSerializer.Deserialize<NetworkHelperMessage>(line);
            if (message?.Kind == "traffic" && message.TrafficEvent is not null)
                TrafficReceived?.Invoke(this, message.TrafficEvent);
            else if (message?.Kind == "status")
                StatusChanged?.Invoke(this, message.Message);
        }
        if (!cancellationToken.IsCancellationRequested)
        {
            StatusChanged?.Invoke(this, "网络监控已停止，可重新启动。");
            Stopped?.Invoke(this, EventArgs.Empty);
        }
    }
}
