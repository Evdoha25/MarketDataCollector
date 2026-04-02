public interface ITickRepository
{
    Task SaveBatchAsync(IReadOnlyList<NormalizedTick> ticks, CancellationToken cancellationToken);
}