using FluidicML.Gain.DTO;
using SocketIO.Core;
using SocketIOClient;
using System.IO.IsolatedStorage;

namespace FluidicML.Gain;

public sealed class SocketAdapter
{
    private readonly ILogger<SocketAdapter> _logger;
    private readonly IConfiguration _config;
    private readonly DentrixAdapter _dentrix;

    private string _apiKey;
    private SocketIOClient.SocketIO? _socket;

    /// <summary>
    /// Used to cancel in-flight websocket requests.
    /// </summary>
    private CancellationTokenSource? _emitTokenSource;

    public bool IsConnected
    {
        get => !string.IsNullOrEmpty(_apiKey) && _socket?.Connected == true;
    }

    public SocketAdapter(
        ILogger<SocketAdapter> logger,
        IConfiguration config,
        DentrixAdapter dentrix
    )
    {
        _logger = logger;
        _config = config;
        _dentrix = dentrix;

        _apiKey = ReadIsolatedStorage();
        _socket = null;
        _emitTokenSource = null;
    }

    private string ReadIsolatedStorage()
    {
        var fileName = _config.GetValue<string>("Storage:SocketFile")!;

        using IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();

        try
        {
            if (storage.FileExists(fileName))
            {
                using IsolatedStorageFileStream stream = storage.OpenFile(fileName, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);

                var encoded = reader.ReadLine();
                if (encoded == null)
                {
                    return string.Empty;
                }
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Could not read from isolated storage at: {time}", DateTimeOffset.Now);
        }

        return string.Empty;
    }

    private void WriteIsolatedStorage()
    {
        var fileName = _config.GetValue<string>("Storage:SocketFile")!;

        using IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();
        using IsolatedStorageFileStream stream = storage.OpenFile(fileName, FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(stream);

        var encoded = System.Text.Encoding.UTF8.GetBytes(_apiKey);
        writer.WriteLine(Convert.ToBase64String(encoded));
        writer.Flush();
    }

    public void Initialize(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            // Periodically check that our socket is still online. This may be redundant.
            // After all, the socket itself is configured to automatically reconnect on
            // failures. But if the external library we're using throws an exception for
            // some reason, we want to recover.
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    if (_socket == null)
                    {
                        await Connect(_apiKey, stoppingToken);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Error connecting websocket to server at: {time}", DateTimeOffset.Now);
                    await Disconnect();
                }

                await Task.Delay(10_000, stoppingToken);
            }
        }, stoppingToken);
    }

    /// <summary>
    /// Ensures only one connection request happens at a time.
    /// </summary>
    private static readonly SemaphoreSlim _semConnect = new(1, 1);

    public async Task Connect(string apiKey, CancellationToken stoppingToken)
    {
        await _semConnect.WaitAsync(stoppingToken);

        try
        {
            if (_socket != null && _apiKey == apiKey)
            {
                return;
            }

            await Disconnect();

            _apiKey = apiKey;
            WriteIsolatedStorage();

            if (string.IsNullOrEmpty(apiKey))
            {
                return;
            }

            // If a socket were to be re-initialized, all in-flight requests should be
            // canceled. Use the following token to make this cancellation work.
            _emitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

            var emitToken = _emitTokenSource.Token;

            _socket = new SocketIOClient.SocketIO(
                _config.GetValue<string>("WebSocketUrl"),
                new SocketIOOptions
                {
                    EIO = EngineIO.V4,
                    Reconnection = true,
                    ReconnectionAttempts = int.MaxValue,
                    ReconnectionDelay = 5000, // Milliseconds
                    ConnectionTimeout = TimeSpan.FromSeconds(10.0),
                    AutoUpgrade = true,
                    Transport = SocketIOClient.Transport.TransportProtocol.WebSocket,
                    Auth = new Dictionary<string, string>() { { "apiKey", _apiKey } }
                });

            _socket.OnConnected += (sender, e) =>
            {
                _logger.LogInformation("Connected at: {time}", DateTimeOffset.Now);
            };

            _socket.OnDisconnected += (sender, e) =>
            {
                _logger.LogInformation("Disconnected \"{e}\" at: {time}", e, DateTimeOffset.Now);
            };

            _socket.OnReconnectAttempt += (sender, attempt) =>
            {
                _logger.LogDebug("Reconnect attempt {attempt} at: {time}", attempt, DateTimeOffset.Now);
            };

            _socket.OnError += (sender, e) =>
            {
                _logger.LogError("Error \"{e}\" at: {time}", e, DateTimeOffset.Now);
            };

            _socket.OnPing += (sender, e) =>
            {
                _logger.LogDebug("Ping at: {time}", DateTimeOffset.Now);
            };

            _socket.OnPong += (sender, e) =>
            {
                _logger.LogDebug("Pong at: {time}", DateTimeOffset.Now);
            };

            _socket.On("query", async (response) =>
            {
                var queryDto = response.GetValue<QueryDto>();

                await foreach (var result in _dentrix.Query(queryDto.query, emitToken))
                {
                    // Calling this function with a cancellation token breaks for some reason.
                    // Since emittance should be quick and the cancellation token is tested
                    // on each iteration, seems safe to just remove. If you do decide to add
                    // it though, keep in mind the token is the *second* argument, not the
                    // last.
                    await _socket.EmitAsync(
                        "query-result",
                        new QueryResultDto()
                        {
                            id = queryDto.id,
                            value = result.value,
                            status = (int)result.status
                        }
                    );

                    // Too fast and we risk overflowing buffer queues in our websocket
                    // server. Should probably play around with this value a bit. Better
                    // fix is either batching results or building out infrastructure that
                    // can handle large streams of data.
                    await Task.Delay(500);
                }
            });

            _logger.LogInformation("Initiating websocket at: {time}", DateTimeOffset.Now);
            await _socket.ConnectAsync(emitToken);
        }
        finally
        {
            _semConnect.Release();
        }
    }

    private async Task Disconnect()
    {
        if (_emitTokenSource != null)
        {
            _emitTokenSource?.Cancel();
            _emitTokenSource?.Dispose();
            _emitTokenSource = null;
        }

        if (_socket != null)
        {
            try
            {
                await _socket.DisconnectAsync();
            }
            finally
            {
                _socket.Dispose();
                _socket = null;
            }
        }
    }
}
