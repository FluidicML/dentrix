using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Pipes;

namespace FluidicML.Gain.Hosting;

public enum Status
{
    HEALTHY = 0,
    UNHEALTHY = 1,
    INDETERMINATE = 2,
    LOCKED = 3,
}

/// <summary>
/// Primary means of communicating to and from the background service.
/// </summary>
public sealed class PipeService(
    ILogger<PipeService> _logger,
    IConfiguration _config
)
{
    private const int TIMEOUT_MILLIS = 2500;

    public async Task SendApiKey(string apiKey)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            _config.GetValue<string>("PipeServer")!,
            PipeDirection.Out
        );
        await pipeClient.ConnectAsync(TIMEOUT_MILLIS);

        var writer = new StreamWriter(pipeClient);
        await writer.WriteLineAsync($"Api {apiKey}");
        await writer.FlushAsync();
    }

    public async Task SendDentrixConnStr(string connStr, CancellationToken stoppingToken)
    {
        await using var pipeClient = new NamedPipeClientStream(
            ".",
            _config.GetValue<string>("PipeServer")!,
            PipeDirection.Out
        );
        await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);

        var writer = new StreamWriter(pipeClient);
        await writer.WriteLineAsync($"Dentrix {connStr}");
        await writer.FlushAsync(stoppingToken);
    }

    private static readonly SemaphoreSlim _backgroundServiceSemaphore = new(1, 1);

    /// <summary>
    /// Verifies our application can connect to the background service.
    /// </summary>
    /// <returns></returns>
    public async Task<Status> QueryBackgroundServiceStatus(CancellationToken stoppingToken)
    {
        var locked = await _backgroundServiceSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return Status.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(
                ".",
                _config.GetValue<string>("PipeServer")!,
                PipeDirection.InOut
            );
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);
            return Status.HEALTHY;
        }
        catch (TimeoutException)
        {
            return Status.UNHEALTHY;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return Status.INDETERMINATE;
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
    public async Task<Status> QueryWebSocketStatus(CancellationToken stoppingToken)
    {
        var locked = await _webSocketSemaphore.WaitAsync(0, stoppingToken);

        System.Diagnostics.Debugger.Launch();

        if (!locked)
        {
            return Status.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(
                ".",
                _config.GetValue<string>("PipeServer")!,
                PipeDirection.InOut
            );
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusWebSocket");
            await writer.FlushAsync(stoppingToken);

            var response = await reader.ReadLineAsync(stoppingToken);

            if (response == "1")
            {
                return Status.HEALTHY;
            }
            else if (response == "0")
            {
                return Status.UNHEALTHY;
            }

            _logger.LogWarning(
                "Unexpected response {response} on `StatusWebSocket` request at: {time}.",
                response,
                DateTimeOffset.Now
            );

            return Status.INDETERMINATE;
        }
        catch (TimeoutException)
        {
            return Status.INDETERMINATE;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return Status.INDETERMINATE;
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
    public async Task<Status> QueryDentrixStatus(CancellationToken stoppingToken)
    {
        var locked = await _dentrixSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return Status.LOCKED;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(
                ".",
                _config.GetValue<string>("PipeServer")!,
                PipeDirection.InOut
            );
            await pipeClient.ConnectAsync(TIMEOUT_MILLIS, stoppingToken);

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusDentrix");
            await writer.FlushAsync(stoppingToken);

            var response = await reader.ReadLineAsync(stoppingToken);

            if (response == "1")
            {
                return Status.HEALTHY;
            }
            else if (response == "0")
            {
                return Status.UNHEALTHY;
            }

            _logger.LogWarning(
                "Unexpected response {response} on `StatusDentrix` request at: {time}.",
                response,
                DateTimeOffset.Now
            );

            return Status.INDETERMINATE;
        }
        catch (TimeoutException)
        {
            return Status.INDETERMINATE;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Unexpected error at: {time}", DateTimeOffset.Now);
            return Status.INDETERMINATE;
        }
        finally
        {
            _dentrixSemaphore.Release();
        }
    }
}
