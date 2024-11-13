using System.Data.Odbc;
using System.Runtime.CompilerServices;
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

    private readonly ILogger<DatabaseAdapter> _logger;

    private readonly string _connectionStr = string.Empty;

    private const string DtxKey = "MNCN5L2G.dtxkey";

    private const string DtxUser = "MNCN5L2G";

    private const string DtxPassword = "MNCN5L2G5";

    public DatabaseAdapter(ILogger<DatabaseAdapter> logger)
    {
        _logger = logger;

        // TODO: Use DtxApi and find location from installation.
        IntPtr hModule = LoadLibrary("C:\\Program Files (x86)\\Dentrix\\Dentrix.API.dll");

        if (hModule == IntPtr.Zero)
        {
            throw new InvalidProgramException("Could not load Dentrix.API.dll.");
        }

        _logger.LogInformation("Attempting to connect to Dentrix.");

        DENTRIXAPI_RegisterUser(Path.GetFullPath(Path.Combine(".", "Assets", DtxKey)));

        _logger.LogInformation("Registered user to Dentrix.API.");

        var connectionStrBuilder = new StringBuilder(512);

        DENTRIXAPI_GetConnectionString(DtxUser, DtxPassword, connectionStrBuilder, 512);

        // This string is only set on a DDP-signed application.
        _connectionStr = connectionStrBuilder.ToString();

        if (string.IsNullOrEmpty(_connectionStr))
        {
            throw new InvalidOperationException("Empty connection string to Dentrix API.");
        }
    }

    private const int MAX_COLUMNS = 256;

    public async IAsyncEnumerable<IDictionary<string, object>> Query(
        string query,
        [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        using var conn = new OdbcConnection(_connectionStr);

        await conn.OpenAsync(stoppingToken);

        using var command = new OdbcCommand(query, conn);
        using var reader = command.ExecuteReader();

        object[] columns = new object[MAX_COLUMNS];

        while (await reader.ReadAsync(stoppingToken))
        {
            int NumberOfColums = reader.GetValues(columns);

            var json = new Dictionary<string, object> { };

            for (int i = 0; i < NumberOfColums; i++)
            {
                var type = reader.GetFieldType(i);
                json[reader.GetName(i)] = Convert.ChangeType(columns[i], type);
            }

            yield return json;
        }
    }
}
