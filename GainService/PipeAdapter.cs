using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FluidicML.Gain;

public sealed class PipeAdapter(
    ILogger<SocketAdapter> _logger,
    IConfiguration _config,
    DentrixAdapter _dentrix,
    SocketAdapter _socket
)
{
    public void Initialize(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await OpenPipe(stoppingToken);
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Broken named pipe at: {time}", DateTimeOffset.Now);
                }

                await Task.Delay(5_000, stoppingToken);
            }
        }, stoppingToken);
    }

    private async Task OpenPipe(CancellationToken stoppingToken)
    {
        // TODO: We need to update this so that only a Fluidic ML, INC. verified
        // program can read/write to this pipe.
        PipeSecurity pipeSecurity = new();
        var sid = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        pipeSecurity.SetAccessRule(new PipeAccessRule(sid, PipeAccessRights.ReadWrite, AccessControlType.Allow));

        await using var pipeServer = NamedPipeServerStreamAcl.Create(
            _config.GetValue<string>("PipeServer")!,
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
                    await Dispatch(buffer, writer, stoppingToken);
                }
            }
            finally
            {
                pipeServer.Disconnect();
            }
        }
    }

    private async Task Dispatch(
        String buffer,
        StreamWriter writer,
        CancellationToken stoppingToken
    )
    {
        if (buffer.StartsWith("Api "))
        {
            _logger.LogInformation("Api message at: {time}.", DateTimeOffset.Now);

            await _socket.Connect(buffer["Api ".Length..], stoppingToken);
        }
        else if (buffer.StartsWith("Dentrix "))
        {
            _logger.LogInformation("Dentrix message at: {time}.", DateTimeOffset.Now);

            _dentrix.Connect(buffer["Dentrix ".Length..]);
        }
        else if (buffer == "StatusWebSocket")
        {
            _logger.LogInformation("StatusWebSocket message at: {time}.", DateTimeOffset.Now);

            await writer.WriteLineAsync((_socket.IsConnected ? 1 : 0).ToString());
            await writer.FlushAsync(stoppingToken);
        }
        else if (buffer == "StatusDentrix")
        {
            _logger.LogInformation("StatusDentrix message at: {time}.", DateTimeOffset.Now);

            await writer.WriteLineAsync((_dentrix.IsConnected ? 1 : 0).ToString());
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
