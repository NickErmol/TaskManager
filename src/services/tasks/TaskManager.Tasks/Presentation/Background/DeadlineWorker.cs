using TaskManager.Tasks.Application.Services;

namespace TaskManager.Tasks.Presentation.Background;

/// <summary>
/// Runs the deadline scan immediately on startup, then every hour (spec §4.3).
/// The interval is configurable (Deadline:ScanIntervalMinutes) so E2E runs can
/// exercise the deadline-email flow without waiting an hour; production keeps 60.
/// </summary>
public class DeadlineWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DeadlineWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = configuration.GetValue<double?>("Deadline:ScanIntervalMinutes") ?? 60;
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
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
