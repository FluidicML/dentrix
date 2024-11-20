using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace FluidicML.Gain.Hosting;

public sealed class RegistryService
{
    private const string RegCommonPath = @"SOFTWARE\Fluidic ML, INC\Gain";
    private const string RegAuthKeyFile = "auth_key_file";
    private const string RegDentrixExePath = "dentrix_exe_path";

    public readonly string AuthKeyFile;
    public readonly string DentrixExePath;

    public RegistryService(ILogger<RegistryService> logger)
    {
        AuthKeyFile = HKLUSoftwareGetValue(logger, RegAuthKeyFile)?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(AuthKeyFile))
        {
#if !Debug
            throw new InvalidProgramException($"Missing registry key \"{RegAuthKeyFile}\".");
#endif
        }

        DentrixExePath = HKLUSoftwareGetValue(logger, RegDentrixExePath)?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(DentrixExePath))
        {
#if !Debug
            throw new InvalidProgramException($"Missing registry key \"{RegDentrixExePath}\".");
#endif
        }
    }

    private static Object? HKLUSoftwareGetValue(ILogger<RegistryService> logger, string key)
    {
        try
        {
            using var subKey = Registry.LocalMachine.OpenSubKey(RegCommonPath);

            var value = subKey?.GetValue(key);

            if (value != null)
            {
                return value;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Registry (original) error at: {time}", DateTimeOffset.Now);
        }

        return null;
    }
}
