using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class MonitoringWorker : BackgroundService
{
    private readonly ITickCounter _counter;
    private readonly ILogger<MonitoringWorker> _logger;
    private readonly TimeSpan _interval;

    public MonitoringWorker(
        ITickCounter counter,
        IConfiguration configuration,
        ILogger<MonitoringWorker> logger)
    {
        _counter = counter;
        _logger = logger;
        _interval = TimeSpan.FromMilliseconds(
            configuration.GetValue<int>("Processing:MonitorIntervalMs", 5000));
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring started (interval={interval}s)", _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        while(await timer.WaitForNextTickAsync(cancellationToken))
        {
            var snapshot = _counter.GetAndReset();

            var perSource = string.Join(" | ",
                snapshot.BySource.Select(kv => $"{kv.Key}: {kv.Value}"));

            _logger.LogInformation(
                "Stats | {PerSource} | Total: {Total} | Dropped: {Dropped}",
                perSource,
                snapshot.Total,
                snapshot.Dropped
            );
        }
    }
}