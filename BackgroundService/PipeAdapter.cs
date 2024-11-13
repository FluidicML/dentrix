using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FluidicML.Gain;

public sealed class PipeAdapter(
    ILogger<SocketAdapter> _logger,
    ConfigViewModel _configProxy,
    DatabaseAdapter _database,
    SocketAdapter _socket
)
{
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

    public async Task Initialize(CancellationToken stoppingToken)
    {
        // TODO: I don't have a full sense of the implications of this security rule.
        // Actually explore this once the design of the application is settled.
        PipeSecurity pipeSecurity = new();
        var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        pipeSecurity.SetAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        await using var pipeServer = NamedPipeServerStreamAcl.Create(
            NAMED_PIPE_SERVER,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Message,
            PipeOptions.Asynchronous,
            1024,
            1024,
            pipeSecurity
        );

        while (!stoppingToken.IsCancellationRequested)
        {
            await pipeServer.WaitForConnectionAsync(stoppingToken);

            try
            {
                var writer = new StreamWriter(pipeServer);
                var reader = new StreamReader(pipeServer);

                string? buffer = await reader.ReadLineAsync(stoppingToken);
                if (!String.IsNullOrEmpty(buffer))
                {
                    await Dispatch(buffer, writer, reader, stoppingToken);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not read/write to named pipe at: {time}.", DateTimeOffset.Now);
            }
            finally
            {
                pipeServer.Disconnect();
            }            
        }
    }

    private async Task Dispatch(String buffer, StreamWriter writer, StreamReader reader, CancellationToken stoppingToken)
    {
        try
        {
            if (buffer.StartsWith("Api "))
            {
                _logger.LogInformation("Updated API key at: {time}.", DateTimeOffset.Now);

                _configProxy.ApiKey = buffer["Api ".Length..];

                await _socket.Initialize(_configProxy.ApiKey, stoppingToken);
            }
            else if (buffer == "Status")
            {
                _logger.LogInformation("Requested status at: {time}.", DateTimeOffset.Now);

                var IsApiKeySet = String.IsNullOrEmpty(_configProxy.ApiKey) ? 0 : 1;
                var IsDbConnected = _database.IsConnected ? 1 : 0;

                await writer.WriteLineAsync($"Api={IsApiKeySet},Db={IsDbConnected}");
                await writer.FlushAsync(stoppingToken);
            }
            else
            {
                _logger.LogError(
                    "Received malformed message {msg} at: {time}.",
                    buffer,
                    DateTimeOffset.Now
                );
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
