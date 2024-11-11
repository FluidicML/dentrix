using SocketIO.Core;
using SocketIOClient;

namespace DentrixService;

public sealed class WindowsBackgroundService : BackgroundService
{
    private readonly ILogger<WindowsBackgroundService> _logger;
    private readonly Config _config;
    private readonly DatabaseAdapter _adapter;

    private SocketIOClient.SocketIO? _socketIO = null;

    public WindowsBackgroundService(
        ILogger<WindowsBackgroundService> logger,
        Config config,
        DatabaseAdapter adapter
    )
    {
        _logger = logger;
        _config = config;
        _adapter = adapter;
    }

    private async Task InitSocketIO(CancellationToken stoppingToken)
    {
        if (String.IsNullOrEmpty(_config.ApiKey))
        {
            return;
        }

        if (_socketIO != null)
        {
            _socketIO.Dispose();
            _socketIO = null;
        }

        _socketIO = new SocketIOClient.SocketIO(_config.WsUrl, new SocketIOOptions
        {
            EIO = EngineIO.V4,
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay = 5000, // Milliseconds
            ConnectionTimeout = TimeSpan.FromSeconds(10.0),
            AutoUpgrade = true,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Auth = new Dictionary<string, string>()
                {
                    { "apiKey", _config.ApiKey }
                }
        });

        _socketIO.OnConnected += (sender, e) =>
        {
            _logger.LogInformation("Connected at: {time}", DateTimeOffset.Now);
        };

        _socketIO.OnDisconnected += (sender, e) =>
        {
            _logger.LogInformation("Disconnected \"{e}\" at: {time}", e, DateTimeOffset.Now);
        };

        _socketIO.OnReconnectAttempt += (sender, attempt) =>
        {
            _logger.LogInformation("Reconnect attempt {attempt} at: {time}", attempt, DateTimeOffset.Now);
        };

        _socketIO.OnError += (sender, e) =>
        {
            _logger.LogError("Error \"{e}\" at: {time}", e, DateTimeOffset.Now);
        };

        _socketIO.OnPing += (sender, e) =>
        {
            _logger.LogInformation("Ping at: {time}", DateTimeOffset.Now);
        };

        _socketIO.OnPong += (sender, e) =>
        {
            _logger.LogInformation("Pong at: {time}", DateTimeOffset.Now);
        };

        _logger.LogInformation("Initiating connection at: {time}", DateTimeOffset.Now);

        await _socketIO.ConnectAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            if (_logger.IsEnabled(LogLevel.Debug))
            {
                _logger.LogDebug("Worker running at: {time}", DateTimeOffset.Now);
            }

            await InitSocketIO(stoppingToken);

            // Keep our service alive even when our websocket isn't initiating.
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
