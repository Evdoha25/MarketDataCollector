public interface IDeduplicator
{
    bool IsNew(NormalizedTick tick);
}