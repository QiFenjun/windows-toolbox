namespace WindowsToolbox.Modules.NetworkTraffic.Services;

/// <summary>固定容量缓冲区，防止实时曲线随运行时间无界增长。</summary>
public sealed class FixedRingBuffer<T>(int capacity)
{
    private readonly Queue<T> _items = new(capacity);

    public int Capacity { get; } = Math.Max(1, capacity);
    public int Count => _items.Count;

    public void Add(T item)
    {
        if (_items.Count == Capacity)
            _items.Dequeue();
        _items.Enqueue(item);
    }

    public IReadOnlyList<T> Snapshot() => _items.ToArray();
}
