using Microsoft.Win32;
using System.Reflection;

namespace FluidicML.Gain;

public sealed class DatabaseAdapter(ILogger<DatabaseAdapter> logger)
{
    public async Task ConnectAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidProgramException("Attempting to run on a non-Windows machine.");
        }

        // TODO: This has to be specified at installation time.
        var exePath = "C:\\Program Files (x86)\\Dentrix";

        if (exePath is string exe)
        {
            logger.LogInformation("Dentrix found at {path}.", exe);

            var DLL = Assembly.LoadFile(Path.Combine(exe, "Dentrix.API.dll"));

            foreach (Type type in DLL.GetExportedTypes())
            {
                logger.LogDebug("DLL exports type {name}.", type.Name);
            }
        }
        else
        {
            throw new InvalidProgramException("Unexpected Dentrix registry key format.");
        }
    }
}
