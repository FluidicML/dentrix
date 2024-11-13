using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FluidicML.Gain;

public sealed class WindowsBackgroundService(
    ILogger<WindowsBackgroundService> _logger,
    ConfigProxy _configProxy,
    DatabaseAdapter _database,
    SocketAdapter _socket
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

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // TODO: I don't have a full sense of the implications of this security rule.
                    // Actually explore this once the design of the application is settled.
                    PipeSecurity pipeSecurity = new();
                    var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
                    pipeSecurity.SetAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

                    // This named pipe communicates with Application.
                    await using var server = NamedPipeServerStreamAcl.Create(
                        NAMED_PIPE_SERVER,
                        PipeDirection.In,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Message,
                        PipeOptions.Asynchronous,
                        1024,
                        1024,
                        pipeSecurity
                    );

                    _logger.LogInformation("Waiting for frontend connection at: {time}.", DateTimeOffset.Now);

                    await server.WaitForConnectionAsync(stoppingToken);

                    using var reader = new StreamReader(server);

                    string? buffer = null;
                    while ((buffer = reader.ReadLine()) != null)
                    {
                        if (buffer.StartsWith("Api "))
                        {
                            var apiKey = buffer["Api ".Length..];

                            _logger.LogInformation("Updated API key at: {time}.", DateTimeOffset.Now);
                            _configProxy.ApiKey = apiKey;

                            await _socket.Initialize(apiKey, stoppingToken);
                        }
                        else
                        {
                            _logger.LogError("Received malformed message {message} at: {time}.", buffer, DateTimeOffset.Now);
                        }
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
