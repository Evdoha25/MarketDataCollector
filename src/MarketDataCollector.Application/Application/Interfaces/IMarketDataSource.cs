public interface IMarketDataSource
{
    SourceType Source { get; }

    IAsyncEnumerable<RawTick> StreamAsync(CancellationToken cancellationToken);
}