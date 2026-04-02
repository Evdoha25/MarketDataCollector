using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class BatchWriterWorker : BackgroundService
{
    private readonly ChannelReader<NormalizedTick> _reader;
    private readonly ITickRepository _repository;
    private readonly ILogger<BatchWriterWorker> _logger;
    private readonly int _batchSize;
    private readonly TimeSpan _flushInterval;

    public BatchWriterWorker(
        Channel<NormalizedTick> normalizedChannel,
        ITickRepository repository,
        IConfiguration configuration,
        ILogger<BatchWriterWorker> logger)
    {
        _reader = normalizedChannel.Reader;
        _repository = repository;
        _logger = logger;

        var section = configuration.GetSection("Processing");
        _batchSize = section.GetValue<int>("BatchSize", 50);
        _flushInterval = TimeSpan.FromMilliseconds(
            section.GetValue<int>("FlushIntervalMs", 1000));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "BatchWriter starting (batch={BatchSize}, flush={FlushMs}ms)",
            _batchSize, _flushInterval.TotalMilliseconds);

        var buffer = new List<NormalizedTick>(_batchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                buffer.Clear();
                await FillBufferAsync(buffer, cancellationToken);

                if(buffer.Count > 0)
                {
                    await _repository.SaveBatchAsync(buffer, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save batch of {Count} ticks", buffer.Count);
            }
        }

        await FlushRemainingAsync();

        _logger.LogInformation("BatchWriter stopped");
    }

    private async Task FillBufferAsync(List<NormalizedTick> buffer, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_flushInterval);

        try
        {
            while(buffer.Count < _batchSize)
            {
                var tick = await _reader.ReadAsync(timeoutCts.Token);
                buffer.Add(tick);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            
        }
        catch (ChannelClosedException)
        {
            
        }
    }

    private async Task FlushRemainingAsync()
    {
        var remaining = new List<NormalizedTick>();

        while (_reader.TryRead(out var tick))
        {
            remaining.Add(tick);
        }

        if (remaining.Count > 0)
        {
            try
            {
                await _repository.SaveBatchAsync(remaining, CancellationToken.None);
                _logger.LogInformation("Flushed remaining {Count} ticks", remaining.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to flush remaining {Count} ticks", remaining.Count);
            }
        }
    }
}