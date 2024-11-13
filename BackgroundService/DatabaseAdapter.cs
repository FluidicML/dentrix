using System.Data.Odbc;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

namespace FluidicML.Gain;

public sealed class DatabaseAdapter
{
    // DENTRIXAPI_RegisterUser status codes.
    private const int RU_SUCCESS = 0;
    private const int RU_USER_CANCELED = 1;
    private const int RU_INVALID_AUTH = 2;
    private const int RU_INVALID_FILE = 3;
    private const int RU_NO_CONNECT = 4;
    private const int RU_LOCAL_RIGHTS_UNSECURED = 5;
    private const int RU_USER_INSERT_FAILED = 6;
    private const int RU_USER_ACCESS_REVOKED = 7;
    private const int RU_INVALID_CERT = 8;
    private const int RU_DATABASE_EX = 9;
    private const int RU_UNKNOWN_ERROR = -1;
    private const int RU_UNSET = -2;

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

    private string _connectionStr = string.Empty;

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
    }

    public async Task Initialize()
    {
        var status = RU_UNSET;
        var authFilePath = Path.GetFullPath(Path.Combine(".", "Assets", DtxKey));

        while (
            status == RU_USER_CANCELED ||
            status == RU_NO_CONNECT ||
            status == RU_DATABASE_EX ||
            status == RU_UNKNOWN_ERROR ||
            status == RU_UNSET
        )
        {
            if (status != RU_UNSET)
            {
                await Task.Delay(5000);
            }

            status = DENTRIXAPI_RegisterUser(authFilePath);

            switch (status)
            {
                case RU_SUCCESS:
                    {
                        _logger.LogInformation("Successfully registered user to Dentrix.");
                        break;
                    }
                default:
                    {
                        _logger.LogError(
                            "Dentrix \"{message}\" at: {time}",
                            RegisterUserErrorMessage(status, authFilePath),
                            DateTimeOffset.Now
                        );
                        break;
                    }
            }
        }

        if (status != RU_SUCCESS)
        {
            throw new InvalidProgramException(RegisterUserErrorMessage(status, authFilePath));
        }

        lock (_connectionStr)
        {
            var builder = new StringBuilder(512);

            DENTRIXAPI_GetConnectionString(DtxUser, DtxPassword, builder, 512);

            // This string is only set on a DDP-signed application.
            _connectionStr = builder.ToString();

            if (string.IsNullOrEmpty(_connectionStr))
            {
                throw new InvalidOperationException("Empty connection string to Dentrix API.");
            }
        }
    }

    private static string RegisterUserErrorMessage(int status, string authFilePath)
    {
        // Messages are copied from DDP documentation verbatim.
        switch (status)
        {
            case RU_USER_CANCELED:
                {
                    return "User canceled Auth";
                }
            case RU_INVALID_AUTH:
                {
                    return "Invalid Auth request";
                }
            case RU_INVALID_FILE:
                {
                    return "Invalid File Auth File " + authFilePath;
                }
            case RU_NO_CONNECT:
                {
                    return "Unable to connect to DB.";
                }
            case RU_LOCAL_RIGHTS_UNSECURED:
                {
                    return "Local admin rights could not be secured.";
                }
            case RU_USER_INSERT_FAILED:
                {
                    return "User insertion failed.";
                }
            case RU_USER_ACCESS_REVOKED:
                {
                    return "User access has been revoked.";
                }
            case RU_INVALID_CERT:
                {
                    return "Invalid Certificate.";
                }
            case RU_DATABASE_EX:
                {
                    return "Database is in exclusive mode.";
                }
            case RU_UNKNOWN_ERROR:
                {
                    return "General Failure to load local requirements";
                }
            default:
                {
                    return "";
                }
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
