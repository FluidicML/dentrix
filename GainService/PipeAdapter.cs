using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FluidicML.Gain;

public sealed class PipeAdapter(
    ILogger<SocketAdapter> _logger,
    DentrixAdapter _database,
    SocketAdapter _socket
)
{
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

    public void Initialize(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
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
                try
                {
                    await pipeServer.WaitForConnectionAsync(stoppingToken);

                    var writer = new StreamWriter(pipeServer);
                    var reader = new StreamReader(pipeServer);

                    string? buffer = await reader.ReadLineAsync(stoppingToken);
                    if (!string.IsNullOrEmpty(buffer))
                    {
                        await Dispatch(buffer, writer, reader, stoppingToken);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Could not read/write to named pipe at: {time}.", DateTimeOffset.Now);
                }
                finally
                {
                    pipeServer.Disconnect();
                }
            }
        }, stoppingToken);
    }

    private async Task Dispatch(String buffer, StreamWriter writer, StreamReader reader, CancellationToken stoppingToken)
    {
        if (buffer.StartsWith("Api "))
        {
            _logger.LogInformation("Updated API key at: {time}.", DateTimeOffset.Now);

            await _socket.Initialize(buffer["Api ".Length..], stoppingToken);
        }
        else if (buffer == "Status")
        {
            _logger.LogInformation("Requested status at: {time}.", DateTimeOffset.Now);

            var IsWsConnected = _socket.IsConnected ? 1 : 0;
            var IsDbConnected = _database.IsConnected ? 1 : 0;

            await writer.WriteLineAsync($"Ws={IsWsConnected},Db={IsDbConnected}");
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
}
