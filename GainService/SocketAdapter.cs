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
                    await Connect(_apiKey, stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Websocket error at: {time}", DateTimeOffset.Now);
                }

                await Task.Delay(30_000, stoppingToken);
            }
        }, stoppingToken);
    }

    // Too fast and we risk overflowing buffer queues in our websocket server. Should probably play
    // around with this value a bit. Better fix is either batching results or building out infrastructure
    // that can handle large streams of data.
    private const int emitThrottleDelay = 500;

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

            // Create a new token source specifically for this socket instance.
            // On reconnect, we should cancel it.
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
                _logger.LogInformation("Reconnect attempt {attempt} at: {time}", attempt, DateTimeOffset.Now);
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

            // Do not include cancellation tokens in the `EmitAsync` calls (commented out).
            // A few outstanding issues in the the library's GitHub issues, but it seems
            // to somehow corrupt the response. Since we have cancellation in the foreach
            // loop and the socket handlers should be cleaned up regardless, it fortunately
            // doesn't seem all that necessary anyways.

            _socket.On("jwt-query", async (response) =>
            {
                System.Diagnostics.Debugger.Launch();
                var jwtQueryDto = response.GetValue<JwtQueryDto>();

                await foreach (var result in _dentrix.Query(jwtQueryDto.query, emitToken))
                {
                    await _socket.EmitAsync(
                        "jwt-query-result",
                        // emitToken,
                        new JwtQueryResultDto()
                        {
                            uuid = jwtQueryDto.uuid,
                            nonce = jwtQueryDto.nonce,
                            value = result.value,
                            status = (int)result.status,
                            message = result.message
                        }
                    );
                }

                await Task.Delay(emitThrottleDelay, emitToken);
            });

            _socket.On("adapter-query", async (response) =>
            {
                var adapterQueryDto = response.GetValue<AdapterQueryDto>();

                await foreach (var result in _dentrix.Query(adapterQueryDto.query, emitToken))
                {
                    await _socket.EmitAsync(
                        "adapter-query-result",
                        // emitToken,
                        new AdapterQueryResultDto()
                        {
                            id = adapterQueryDto.id,
                            value = result.value,
                            status = (int)result.status,
                            message = result.message
                        }
                    );

                    await Task.Delay(emitThrottleDelay, emitToken);
                }
            });

            _logger.LogInformation("Initiating websocket at: {time}", DateTimeOffset.Now);

            // Connection attempts run in the background. This `await` call finishes quickly.
            // Mirrors how the default `ConnectAsync` constructor works when passing in our
            // own cancellation token.
            using var connectTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
            await _socket.ConnectAsync(connectTokenSource.Token);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await Disconnect();
            throw;
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
            try
            {
                // Intentionally avoid calling `Dispose` in this case. Any outstanding
                // tasks that rely on this need the token to continue existing beforehand.
                // We'll trust the GC to eventually kick in here.
                _emitTokenSource.Cancel();
            }
            finally
            {
                _emitTokenSource = null;
            }
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
