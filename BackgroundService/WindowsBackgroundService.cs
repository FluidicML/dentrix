using SocketIO.Core;
using SocketIOClient;
using System.IO.Pipes;

namespace FluidicML.Gain;

public sealed class WindowsBackgroundService(
    ILogger<WindowsBackgroundService> logger,
    ConfigProxy configProxy,
    DatabaseAdapter adapter
) : BackgroundService
{
    // TODO: We need to secure this. As of now, anyone could write to this
    // server and we blindly accept this. How we go about securing this isn't
    // exactly clear though.
    private static readonly string NAMED_PIPE_SERVER = "gain-dentrix";

    private SocketIOClient.SocketIO? _socketIO = null;

    private async Task InitSocketIO(string apiKey, CancellationToken stoppingToken)
    {
        if (_socketIO != null)
        {
            await _socketIO.DisconnectAsync();
            _socketIO.Dispose();
            _socketIO = null;
        }

        _socketIO = new SocketIOClient.SocketIO(configProxy.WsUrl, new SocketIOOptions
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
                    { "apiKey", apiKey }
                }
        });

        _socketIO.OnConnected += (sender, e) =>
        {
            logger.LogInformation("Connected at: {time}", DateTimeOffset.Now);
        };

        _socketIO.OnDisconnected += (sender, e) =>
        {
            logger.LogInformation("Disconnected \"{e}\" at: {time}", e, DateTimeOffset.Now);
        };

        _socketIO.OnReconnectAttempt += (sender, attempt) =>
        {
            logger.LogInformation("Reconnect attempt {attempt} at: {time}", attempt, DateTimeOffset.Now);
        };

        _socketIO.OnError += (sender, e) =>
        {
            logger.LogError("Error \"{e}\" at: {time}", e, DateTimeOffset.Now);
        };

        _socketIO.OnPing += (sender, e) =>
        {
            logger.LogInformation("Ping at: {time}", DateTimeOffset.Now);
        };

        _socketIO.OnPong += (sender, e) =>
        {
            logger.LogInformation("Pong at: {time}", DateTimeOffset.Now);
        };

        logger.LogInformation("Initiating connection at: {time}", DateTimeOffset.Now);

        await _socketIO.ConnectAsync(stoppingToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            logger.LogInformation("Service started at: {time}", DateTimeOffset.Now);

            // Attempt to load in the Dentrix DLL and connect to the Dentrix database.
            // await adapter.ConnectAsync();

            // If we already have an API key available, we can immediately initialize our connection.
            {
                var apiKey = configProxy.ApiKey;
                if (!string.IsNullOrEmpty(apiKey))
                {
                    await InitSocketIO(apiKey, stoppingToken);
                }
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // This named pipe communicates with Application.
                    await using var server = new NamedPipeServerStream(NAMED_PIPE_SERVER, PipeDirection.In);
                    logger.LogInformation("Waiting for Application connection: {time}.", DateTimeOffset.Now);

                    await server.WaitForConnectionAsync(stoppingToken);

                    using var reader = new StreamReader(server);

                    string? buffer = null;
                    while ((buffer = reader.ReadLine()) != null)
                    {
                        if (buffer.StartsWith("Api "))
                        {
                            var apiKey = buffer["Api ".Length..];

                            logger.LogInformation("Updated API key at: {time}.", DateTimeOffset.Now);
                            configProxy.ApiKey = apiKey;

                            await InitSocketIO(apiKey, stoppingToken);
                        }
                        else
                        {
                            logger.LogError("Received malformed message {message} at: {time}.", buffer, DateTimeOffset.Now);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Intentionally empty.
                }
                catch (Exception e)
                {
                    logger.LogError(e, "Encountered unknown exception.");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Intentionally empty.
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Message}", ex.Message);

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
