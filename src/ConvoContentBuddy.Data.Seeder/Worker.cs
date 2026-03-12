namespace ConvoContentBuddy.Data.Seeder;

/// <summary>
/// Background worker service that handles data seeding operations.
/// </summary>
/// <param name="logger">The logger instance for this worker.</param>
public class Worker(ILogger<Worker> logger) : BackgroundService
{
    /// <summary>
    /// Executes the background data seeding work.
    /// </summary>
    /// <param name="stoppingToken">A token that signals when the service should stop.</param>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Worker running at: {time}", DateTimeOffset.Now);
            }
            await Task.Delay(1000, stoppingToken);
        }
    }
}
