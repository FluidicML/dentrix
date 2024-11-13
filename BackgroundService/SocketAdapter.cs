using FluidicML.Gain.DTO;
using SocketIO.Core;
using SocketIOClient;

namespace FluidicML.Gain;

public sealed class SocketAdapter(
    ILogger<SocketAdapter> _logger,
    ConfigProxy _configProxy,
    DatabaseAdapter _database
)
{
    private SocketIOClient.SocketIO? _socketIO = null;

    public async Task Initialize(string apiKey, CancellationToken stoppingToken)
    {
        if (_socketIO != null)
        {
            await _socketIO.DisconnectAsync();
            _socketIO.Dispose();
            _socketIO = null;
        }

        if (String.IsNullOrEmpty(apiKey))
        {
            return;
        }

        _socketIO = new SocketIOClient.SocketIO(_configProxy.WsUrl, new SocketIOOptions
        {
            EIO = EngineIO.V4,
            Reconnection = true,
            ReconnectionAttempts = int.MaxValue,
            ReconnectionDelay = 5000, // Milliseconds
            ConnectionTimeout = TimeSpan.FromSeconds(10.0),
            AutoUpgrade = true,
            Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
            Auth = new Dictionary<string, string>() { { "apiKey", apiKey } }
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

        _socketIO.On("query", async (response) =>
        {
            var queryDto = response.GetValue<QueryDto>();

            await foreach (var row in _database.Query(queryDto.query, stoppingToken))
            {
                await _socketIO.EmitAsync("results", new ResultsDto()
                {
                    id = queryDto.id,
                    results = [row]
                });
            }

            await _socketIO.EmitAsync("finished", new FinishedDto()
            {
                id = queryDto.id
            });
        });

        _logger.LogInformation("Initiating connection at: {time}", DateTimeOffset.Now);

        await _socketIO.ConnectAsync(stoppingToken);
    }
}
