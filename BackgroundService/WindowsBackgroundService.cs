namespace FluidicML.Gain;

public sealed class WindowsBackgroundService(
    ILogger<WindowsBackgroundService> _logger,
    ConfigProxy _configProxy,
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
            await _database.Initialize(stoppingToken);
            await _socket.Initialize(_configProxy.ApiKey, stoppingToken);

            await foreach (var msg in _pipe.Listen(stoppingToken))
            {
                try
                {
                    if (msg.StartsWith("Api "))
                    {
                        _configProxy.ApiKey = msg["Api ".Length..];

                        _logger.LogInformation("Updated API key at: {time}.", DateTimeOffset.Now);

                        await _socket.Initialize(_configProxy.ApiKey, stoppingToken);
                    }
                    else
                    {
                        _logger.LogError("Received malformed message {msg} at: {time}.", msg, DateTimeOffset.Now);
                    }
                }
                catch (OperationCanceledException)
                {
                    // Intentionally empty.
                }
                catch (Exception e)
                {
                    _logger.LogError(e, "Encountered unknown exception at: {time}.", DateTimeOffset.Now);

                    await Task.Delay(5000, stoppingToken);
                }
            }
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
