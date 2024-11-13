using Microsoft.Win32;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace FluidicML.Gain;

public sealed class DatabaseAdapter
{
    [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr LoadLibrary(string dllName);

    private const string DtxAPI = "Dentrix.API.dll";

    [DllImport(DtxAPI, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern int DENTRIXAPI_RegisterUser([MarshalAs(UnmanagedType.LPStr)] string szKeyFilePath);

    [DllImport(DtxAPI, CharSet = CharSet.Ansi, CallingConvention = CallingConvention.StdCall)]
    private static extern void DENTRIXAPI_GetConnectionString(
        [MarshalAs(UnmanagedType.LPStr)] string szUserId,
        [MarshalAs(UnmanagedType.LPStr)] string szPassword,
        StringBuilder szConnectionsString,
        int ConnectionStringSize
    );

    private ILogger<DatabaseAdapter> _logger;

    public DatabaseAdapter(ILogger<DatabaseAdapter> logger)
    {
        _logger = logger;

        // TODO: Use DtxApi and find location from installation.
        IntPtr hModule = LoadLibrary("C:\\Program Files (x86)\\Dentrix\\Dentrix.API.dll");

        if (hModule == IntPtr.Zero)
        {
            _logger.LogError("Could not load Dentrix.API.dll.");
            return;
        }
    }

    private const string DtxKey = "MNCN5L2G.dtxkey";

    private const string DtxUser = "MNCN5L2G";

    private const string DtxPassword = "MNCN5L2G5";

    public async Task ConnectAsync()
    {
        _logger.LogInformation("Attempting to connect to Dentrix.");

        DENTRIXAPI_RegisterUser(Path.Combine(".", "Assets", DtxKey));

        _logger.LogInformation("Registered user to Dentrix.API.");

        var connectionStr = new StringBuilder(512);

        DENTRIXAPI_GetConnectionString(DtxUser, DtxPassword, connectionStr, 512);

        var result = connectionStr.ToString();

        _logger.LogError(result);
    }
}
