using System.Threading.Channels;
using Microsoft.Extensions.Logging;

public class TickProgressService
{
    private readonly ChannelReader<RawTick> _input;
    private readonly ChannelWriter<NormalizedTick> _output;
    private readonly Dictionary<SourceType, ITickNormalizer> _normalizers;
    private readonly IDeduplicator _deduplicator;
    private readonly ITickCounter _counter;
    private readonly ILogger<TickProgressService> _logger;

    public TickProgressService(
        Channel<RawTick> rawChannel,
        Channel<NormalizedTick> normalizedChannel,
        IEnumerable<ITickNormalizer> normalizers,
        IDeduplicator deduplicator,
        ITickCounter counter,
        ILogger<TickProgressService> logger)
    {
        _input = rawChannel.Reader;
        _output = normalizedChannel.Writer;
        _deduplicator = deduplicator;
        _counter = counter;
        _logger = logger;

        _normalizers = normalizers.ToDictionary(n => n.Source);
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing pipeline started");

        await foreach (var raw in _input.ReadAllAsync(cancellationToken))
        {
            try
            {
                ProcessTick(raw);
            }
            catch(Exception ex)
            {
                _logger.LogWarning(ex, "Failed to process tick from {Source}", raw.Source);
                _counter.IncrementDropped();
            }
        }

        _output.Complete();
        _logger.LogInformation("Processing pipeline stopped");
    }

    private void ProcessTick(RawTick raw)
    {
        if (!_normalizers.TryGetValue(raw.Source, out var normalizer))
        {
            _logger.LogWarning("No normalizer for {Source}", raw.Source);
            _counter.IncrementDropped();
            return;
        }

        var normalized = normalizer.TryNormalize(raw);
        if (normalized is null)
        {
            _counter.IncrementDropped();
            return;
        }

        if (!_deduplicator.IsNew(normalized))
        {
            _counter.IncrementDropped();
            return;
        }

        if(!_output.TryWrite(normalized))
        {
            _logger.LogWarning("Ouput channel full, dropping tick");
            _counter.IncrementDropped();
            return;
        }

        _counter.Increment(raw.Source);
    }
}
