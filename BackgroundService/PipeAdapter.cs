using System.IO.Pipes;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace FluidicML.Gain;

public sealed class PipeAdapter(ILogger<SocketAdapter> _logger)
{
    private const string NAMED_PIPE_SERVER = "DB3B88B2-AC72-4B06-893A-89E69E73E134";

    public async IAsyncEnumerable<string> Listen(
        [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        while (!stoppingToken.IsCancellationRequested)
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
                yield return buffer;
            }
        }
    }
}
