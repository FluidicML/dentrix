namespace FluidicML.Gain;

public sealed class WindowsBackgroundService(
    ILogger<WindowsBackgroundService> _logger,
    DatabaseAdapter _database,
    SocketAdapter _socket,
    PipeAdapter _pipe
) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Service started at: {time}", DateTimeOffset.Now);

            // Each adapter runs in their own task. This makes the service possible to
            // stop (e.g. during uninstallation).
            _database.Initialize(stoppingToken);
            _socket.Initialize(stoppingToken);
            _pipe.Initialize(stoppingToken);

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Intentionally empty.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Message}", ex.Message);

            // Terminates this process and returns an exit code to the operating system.
            // This is required to avoid the 'BackgroundServiceExceptionBehavior', which
            // performs one of two scenarios:
            // 1. When set to "Ignore": will do nothing at all, errors cause zombie services.
            // 2. When set to "StopHost": will cleanly stop the host, and log errors.
            //
            // In order for the Windows Service Management system to leverage configured
            // recovery options, we need to terminate the process with a non-zero exit code.
            Environment.Exit(1);
        }
    }
}
