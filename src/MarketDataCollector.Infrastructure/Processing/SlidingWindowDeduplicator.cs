using System.Collections.Concurrent;

public class SlidingWindowDeduplicator : IDeduplicator, IDisposable
{
    private readonly ConcurrentDictionary<string,long> _seen = new();

    private readonly TimeSpan _window;

    private readonly Timer _cleanupTimer;

    public SlidingWindowDeduplicator(TimeSpan? window = null)
    {
        _window = window ?? TimeSpan.FromSeconds(5);

        _cleanupTimer = new Timer(
            callback: _ => Cleanup(),
            state: null,
            dueTime: TimeSpan.FromSeconds(1),
            period: TimeSpan.FromSeconds(1)
        );
    }

    public bool IsNew(NormalizedTick tick)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var key = tick.DeduplicationKey;

        return _seen.TryAdd(key, now);
    }

    private void Cleanup()
    {
        var cutoff = DateTimeOffset.UtcNow
            .Add(-_window)
            .ToUnixTimeMilliseconds();

        foreach (var kvp in _seen)
        {
            if (kvp.Value < cutoff)
            {
                _seen.TryRemove(kvp);
            }
        }
    }

    public void Dispose() => _cleanupTimer.Dispose();
}