namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>在专用线程获取和释放命名 Mutex，避免跨 await 释放导致线程所有权异常。</summary>
public sealed class NamedMutexLease : IDisposable
{
    private readonly ManualResetEventSlim _release = new(false);
    private readonly Thread _ownerThread;
    private bool _disposed;

    private NamedMutexLease(string name, TaskCompletionSource<bool> acquired)
    {
        _ownerThread = new Thread(() => OwnMutex(name, acquired))
        {
            IsBackground = true,
            Name = "WindowsToolbox NetworkTraffic Mutex Owner"
        };
        _ownerThread.Start();
    }

    public static async Task<NamedMutexLease?> TryAcquireAsync(string name, CancellationToken cancellationToken)
    {
        TaskCompletionSource<bool> acquired = new(TaskCreationOptions.RunContinuationsAsynchronously);
        NamedMutexLease lease = new(name, acquired);
        bool ownsMutex;
        try { ownsMutex = await acquired.Task.WaitAsync(cancellationToken).ConfigureAwait(false); }
        catch
        {
            lease.Dispose();
            throw;
        }

        if (ownsMutex)
            return lease;

        lease.Dispose();
        return null;
    }

    private void OwnMutex(string name, TaskCompletionSource<bool> acquired)
    {
        try
        {
            using Mutex mutex = new(false, name);
            bool ownsMutex;
            try { ownsMutex = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { ownsMutex = true; }
            acquired.TrySetResult(ownsMutex);
            if (!ownsMutex)
                return;

            _release.Wait();
            mutex.ReleaseMutex();
        }
        catch (Exception exception)
        {
            acquired.TrySetException(exception);
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _release.Set();
        if (Thread.CurrentThread != _ownerThread)
            _ownerThread.Join(TimeSpan.FromSeconds(5));
        _release.Dispose();
    }
}
