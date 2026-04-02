using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class SourceReaderWorker : BackgroundService
{
    private readonly IEnumerable<IMarketDataSource> _sources;
    private readonly ChannelWriter<RawTick> _writer;
    private readonly ILogger<SourceReaderWorker> _logger;

    public SourceReaderWorker(
        IEnumerable<IMarketDataSource> sources,
        Channel<RawTick> rawChannel,
        ILogger<SourceReaderWorker> logger
    )
    {
        _sources = sources;
        _writer = rawChannel.Writer;
        _logger = logger;        
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SourceReader starting with {Count} sources",
        _sources.Count());

        var tasks = _sources
            .Select(source => ReadSourceAsync(source, cancellationToken))
            .ToList();

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "One or more sources failed");
        }
        finally
        {
            _writer.Complete();
            _logger.LogInformation("SourceReader stopped, channel completed");
        }
    }

    private async Task ReadSourceAsync(IMarketDataSource source, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[{Source}] Reader started", source.Source);

        try
        {
            await foreach (var tick in source.StreamAsync(cancellationToken))
            {
                await _writer.WriteAsync(tick, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{Source}] Reader failed", source.Source);
            throw;
        }

        _logger.LogInformation("[{Source}] Reader stopped", source.Source);
    }
}