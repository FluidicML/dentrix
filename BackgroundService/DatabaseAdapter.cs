using Microsoft.Win32;
using System.Reflection;

namespace DentrixService;

public sealed class DatabaseAdapter(ILogger<DatabaseAdapter> logger)
{
    public async Task ConnectAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new InvalidProgramException("Attempting to run on a non-Windows machine.");
        }

        var dentrixKey = Path.Combine(Registry.CurrentUser.Name, "Software", "Dentrix Dental Systems, Inc.", "Dentrix", "General");
        var exePath = Registry.GetValue(dentrixKey, "ExePath", null);

        if (exePath == null)
        {
            throw new InvalidProgramException("Could not find Dentrix location.");
        }

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
