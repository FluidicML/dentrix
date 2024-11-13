namespace FluidicML.Gain;

public sealed class WindowsBackgroundService(
    ILogger<WindowsBackgroundService> _logger,
    DatabaseAdapter _database,
    SocketAdapter _socket,
    PipeAdapter _pipe
) : BackgroundService
{
    private static readonly string NAMED_PIPE_SERVER = "gain-dentrix";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            _logger.LogInformation("Service started at: {time}", DateTimeOffset.Now);

            // The order we initialize things is important. This reflects the
            // order of our dependency chain.

            _database.Initialize(stoppingToken);
            await _socket.Initialize(stoppingToken);
            await _pipe.Initialize(stoppingToken);
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
