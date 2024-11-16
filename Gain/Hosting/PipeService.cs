using FluidicML.Gain.ViewModels;
using Microsoft.Extensions.Logging;
using System.IO;
using System.IO.Pipes;

namespace FluidicML.Gain.Hosting;

/// <summary>
/// Primary means of communicating to and from the background service.
/// </summary>
public sealed class PipeService(ILogger<PipeService> logger, StatusViewModel statusViewModel)
{
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

    private static readonly SemaphoreSlim _backgroundServiceSemaphore = new(1, 1);

    /// <summary>
    /// Verifies our application can connect to the background service.
    /// </summary>
    /// <returns></returns>
    public async Task QueryBackgroundServiceStatus(CancellationToken stoppingToken)
    {
        var locked = await _backgroundServiceSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(2500, stoppingToken); // Milliseconds timeout
        }
        catch (TimeoutException)
        {
            statusViewModel.StatusBackgroundService = false;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            statusViewModel.StatusBackgroundService = null;
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
    public async Task QueryWebSocketStatus(CancellationToken stoppingToken)
    {
        if (statusViewModel.StatusBackgroundService != true)
        {
            statusViewModel.StatusWebSocket = null;
            return;
        }

        var locked = await _webSocketSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(2500, stoppingToken); // Milliseconds timeout

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusWebSocket");
            await writer.FlushAsync();

            var status = await reader.ReadLineAsync();

            if (status == "1")
            {
                statusViewModel.StatusWebSocket = true;
            }
            else if (status == "0")
            {
                statusViewModel.StatusWebSocket = false;
            }
            else
            {
                logger.LogError("Unexpected response on `StatusWebSocket` request.");
                statusViewModel.StatusWebSocket = null;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            statusViewModel.StatusWebSocket = null;
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
    public async Task QueryDentrixStatus(CancellationToken stoppingToken)
    {
        if (statusViewModel.StatusBackgroundService != true)
        {
            statusViewModel.StatusDentrix = null;
            return;
        }

        var locked = await _dentrixSemaphore.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            return;
        }

        try
        {
            await using var pipeClient = new NamedPipeClientStream(".", NAMED_PIPE_SERVER, PipeDirection.InOut);
            await pipeClient.ConnectAsync(2500, stoppingToken); // Milliseconds timeout

            var writer = new StreamWriter(pipeClient);
            var reader = new StreamReader(pipeClient);

            await writer.WriteLineAsync("StatusDentrix");
            await writer.FlushAsync();

            var status = await reader.ReadLineAsync();

            if (status == "1")
            {
                statusViewModel.StatusDentrix = true;
            }
            else if (status == "0")
            {
                statusViewModel.StatusDentrix = false;
            }
            else
            {
                logger.LogError("Unexpected response on `StatusDentrix` request.");
                statusViewModel.StatusDentrix = null;
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            statusViewModel.StatusDentrix = null;
        }
        finally
        {
            _dentrixSemaphore.Release();
        }
    }
}
