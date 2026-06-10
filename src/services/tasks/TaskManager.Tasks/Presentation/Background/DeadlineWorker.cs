using TaskManager.Tasks.Application.Services;

namespace TaskManager.Tasks.Presentation.Background;

/// <summary>Runs the deadline scan immediately on startup, then every hour (spec §4.3).</summary>
public class DeadlineWorker(IServiceScopeFactory scopeFactory, ILogger<DeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DeadlineScanner>().ScanAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deadline scan failed; will retry next tick");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
