using FluidicML.Gain.DTO;
using SocketIO.Core;
using SocketIOClient;
using System.IO.IsolatedStorage;

namespace FluidicML.Gain;

public sealed class SocketAdapter
{
    private readonly ILogger<SocketAdapter> _logger;
    private readonly IConfiguration _configService;
    private readonly DatabaseAdapter _database;

    public bool IsConnected
    {
        get => _socket?.Connected == true && !string.IsNullOrEmpty(_apiKey);
    }

    private string _apiKey;

    /// <summary>
    /// The websocket connection to the gain backend.
    /// </summary>
    private SocketIOClient.SocketIO? _socket;

    /// <summary>
    /// Ensures only one connection request happens at a time.
    /// </summary>
    private static readonly SemaphoreSlim _semSocket = new(1, 1);

    /// <summary>
    /// Used to cancel in-flight websocket requests.
    /// </summary>
    private CancellationTokenSource? _emitTokenSource;

    public SocketAdapter(
        ILogger<SocketAdapter> logger,
        IConfiguration configService,
        DatabaseAdapter database
    )
    {
        _logger = logger;
        _configService = configService;
        _database = database;

        _apiKey = ReadApiKey() ?? string.Empty;
        _socket = null;
        _emitTokenSource = null;
    }

    public void Initialize(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Initialize(_apiKey, stoppingToken);
                await Task.Delay(10000, stoppingToken);
            }
        }, stoppingToken);
    }

    public async Task Initialize(string apiKey, CancellationToken stoppingToken)
    {
        try
        {
            // We periodically check that our socket is still online. This is
            // potentially redundant - we create a constantly reconnecting websocket
            // already. Still, in case of unforseen errors, this is a good fallback.
            await _semSocket.WaitAsync(stoppingToken);

            if (_socket == null || _apiKey != apiKey)
            {
                await ConnectToServer(apiKey, stoppingToken);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Error connecting websocket to server at: {time}", DateTimeOffset.Now);

            // Need to disconnect here to ensure our socket is set back to null.
            // This way our retry mechanism (created at initialization) can recover.
            await DisconnectFromServer();
        }
        finally
        {
            _semSocket?.Release();
        }
    }

    private async Task DisconnectFromServer()
    {
        if (_emitTokenSource != null)
        {
            _emitTokenSource?.Cancel();
            _emitTokenSource = null;
        }

        if (_socket != null)
        {
            await _socket.DisconnectAsync();
            _socket.Dispose();
            _socket = null;
        }
    }

    private async Task ConnectToServer(string apiKey, CancellationToken stoppingToken)
    {
        await DisconnectFromServer();

        _apiKey = apiKey;

        WriteApiKey();

        if (string.IsNullOrEmpty(apiKey))
        {
            return;
        }

        // If a socket were to be re-initialized, all in-flight requests should be
        // canceled. Use the following token to make this cancellation work.
        _emitTokenSource = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var emitToken = _emitTokenSource.Token;

        _socket = new SocketIOClient.SocketIO(
            _configService.GetValue<string>("WS_URL"),
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
            _logger.LogInformation("Ping at: {time}", DateTimeOffset.Now);
        };

        _socket.OnPong += (sender, e) =>
        {
            _logger.LogInformation("Pong at: {time}", DateTimeOffset.Now);
        };

        _socket.On("query", async (response) =>
        {
            var queryDto = response.GetValue<QueryDto>();

            await foreach (var row in _database.Query(queryDto.query, emitToken))
            {
                if (_socket?.Connected == true)
                {
                    await _socket.EmitAsync("results", new ResultsDto()
                    {
                        id = queryDto.id,
                        results = [row]
                    }, emitToken);
                }
            }

            if (_socket?.Connected == true)
            {
                await _socket.EmitAsync("finished", new FinishedDto()
                {
                    id = queryDto.id
                }, emitToken);
            }
        });

        _logger.LogInformation("Initiating WS connection at: {time}", DateTimeOffset.Now);

        await _socket.ConnectAsync(emitToken);
    }

    private static readonly string PERSISTED_FILENAME = "Config.data";
    private static readonly string API_KEY = "API_KEY";

    private string? ReadApiKey()
    {
        IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();

        try
        {
            if (storage.FileExists(PERSISTED_FILENAME))
            {
                using IsolatedStorageFileStream stream = storage.OpenFile(PERSISTED_FILENAME, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);
                {
                    while (!reader.EndOfStream)
                    {
                        // The value is base64 encoded so no comma can appear.
                        string[]? kv = reader.ReadLine()?.Split([',']);
                        if (kv is not null)
                        {
                            if (kv[0] == API_KEY)
                            {
                                var encoded = Convert.FromBase64String(kv[1]);
                                return System.Text.Encoding.UTF8.GetString(encoded);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not read from isolated storage at: {time}", DateTimeOffset.Now);
        }

        return null;
    }

    private void WriteApiKey()
    {
        IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();
        using IsolatedStorageFileStream stream = storage.OpenFile(PERSISTED_FILENAME, FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(stream);

        var encodedAccessToken = System.Text.Encoding.UTF8.GetBytes(_apiKey);
        writer.WriteLine("{0},{1}", API_KEY, Convert.ToBase64String(encodedAccessToken));
    }
}
