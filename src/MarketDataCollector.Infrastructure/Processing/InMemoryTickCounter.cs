using System.Collections.Concurrent;

public class InMemoryTickCounter : ITickCounter
{
    private readonly ConcurrentDictionary<SourceType, long> _counters = new();

    private long _dropped;

    public void Increment(SourceType source)
    {
        _counters.AddOrUpdate(
            key: source,
            addValue: 1,
            updateValueFactory: (_, old) => old + 1);
    }

    public void IncrementDropped()
    {
        Interlocked.Increment(ref _dropped);
    }

    public TickCounterSnapshot GetAndReset()
    {
        var snapshot = new Dictionary<SourceType, long>();

        foreach (var source in Enum.GetValues<SourceType>())
        {
            if (_counters.TryRemove(source, out var count))
                snapshot[source] = count;
            else
                snapshot[source] = 0;
        }

        var dropped = Interlocked.Exchange(ref _dropped, 0);

        return new TickCounterSnapshot
        {
            BySource = snapshot,
            Dropped = dropped
        };
    }
}