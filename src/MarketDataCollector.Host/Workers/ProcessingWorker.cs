using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

public class ProcessingWorker : BackgroundService
{
    private readonly TickProgressService _progressService;
    private readonly ILogger<ProcessingWorker> _logger;

    public ProcessingWorker(
        TickProgressService progressService,
        ILogger<ProcessingWorker> logger
    )
    {
        _progressService = progressService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing pipeline starting");

        try
        {
            await _progressService.RunAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Processing pipeline failed");
        }

        _logger.LogInformation("Processing pipeline stopped");
    }
}