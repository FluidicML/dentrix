using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Pipes;

namespace FluidicML.Gain.Hosting;

public enum QueryStatus
{
    SUCCESS = 0,
    FAILURE = 1,
    INDETERMINATE = 2,
    LOCKED = 3,
}

/// <summary>
/// Primary means of communicating to and from the background service.
/// </summary>
public sealed class PipeService(ILogger<PipeService> logger)
{
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

    private const int TIMEOUT_MILLIS = 2500;

    public static async Task SendApiKey(string apiKey)
    {
        await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.Out);
        await pipeClient.ConnectAsync(TIMEOUT_MILLIS);

        using var writer = new StreamWriter(pipeClient);
        await writer.WriteLineAsync($"Api {apiKey}");
        await writer.FlushAsync();
    }

    private static readonly SemaphoreSlim _backgroundServiceSemaphore = new(1, 1);

    /// <summary>
    /// Verifies our application can connect to the background service.
    /// </summary>
    /// <returns></returns>
    public async Task<QueryStatus> QueryBackgroundServiceStatus(CancellationToken stoppingToken)
    {
        var locked = await _backgroundServiceSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return QueryStatus.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);
            return QueryStatus.SUCCESS;
        }
        catch (TimeoutException)
        {
            return QueryStatus.FAILURE;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return QueryStatus.INDETERMINATE;
        }
        finally
        {
            _backgroundServiceSemaphore.Release();
        }
    }

    private static readonly SemaphoreSlim _webSocketSemaphore = new(1, 1);

    /// <summary>
    /// Queries our background service on its connection status to Gain servers.
    /// </summary>
    /// <returns></returns>
    public async Task<QueryStatus> QueryWebSocketStatus(CancellationToken stoppingToken)
    {
        var locked = await _webSocketSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return QueryStatus.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusWebSocket");
            await writer.FlushAsync(stoppingToken);

            var status = await reader.ReadLineAsync(stoppingToken);

            if (status == "1")
            {
                return QueryStatus.SUCCESS;
            }
            else if (status == "0")
            {
                return QueryStatus.FAILURE;
            }

            logger.LogError("Unexpected response on `StatusWebSocket` request.");
            return QueryStatus.INDETERMINATE;
        }
        catch (TimeoutException)
        {
            return QueryStatus.INDETERMINATE;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return QueryStatus.INDETERMINATE;
        }
        finally
        {
            _webSocketSemaphore.Release();
        }
    }

    private static readonly SemaphoreSlim _dentrixSemaphore = new(1, 1);

    /// <summary>
    /// Queries our background service on its connection to the Dentrix database.
    /// </summary>
    /// <returns></returns>
    public async Task<QueryStatus> QueryDentrixStatus(CancellationToken stoppingToken)
    {
        var locked = await _dentrixSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return QueryStatus.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusDentrix");
            await writer.FlushAsync(stoppingToken);

            var status = await reader.ReadLineAsync(stoppingToken);

            if (status == "1")
            {
                return QueryStatus.SUCCESS;
            }
            else if (status == "0")
            {
                return QueryStatus.FAILURE;
            }

            logger.LogError("Unexpected response on `StatusDentrix` request.");
            return QueryStatus.INDETERMINATE;
        }
        catch (TimeoutException)
        {
            return QueryStatus.INDETERMINATE;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return QueryStatus.INDETERMINATE;
        }
        finally
        {
            _dentrixSemaphore.Release();
        }
    }
}
