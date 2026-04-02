public interface ITickCounter
{
    void Increment(SourceType source);
    void IncrementDropped();

    TickCounterSnapshot GetAndReset();
}

public sealed class TickCounterSnapshot
{
    public required IReadOnlyDictionary<SourceType, long> BySource { get; set; }
    public required long Dropped { get; set; }
    public long Total => BySource.Values.Sum();
}