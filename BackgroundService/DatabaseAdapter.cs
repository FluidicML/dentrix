using Microsoft.Win32;
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

    private const string RegCommonPath = @"SOFTWARE\Fluidic ML, INC.\Gain";
    private const string RegKeyAuthFile = "auth_key_file";
    private const string RegKeyDtxPath = "dentrix_exe_path";

    private readonly string _regAuthFile;
    private readonly string _regDtxPath;

    public DatabaseAdapter(ILogger<DatabaseAdapter> logger)
    {
        _logger = logger;

        // HKLUSoftwareGetValue depends on logger. Make sure its set first.

        _regAuthFile = HKLUSoftwareGetValue(RegKeyAuthFile)?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(_regAuthFile))
        {
            throw new InvalidProgramException($"Missing registry key \"{RegKeyAuthFile}\".");
        }

        _regDtxPath = HKLUSoftwareGetValue(RegKeyDtxPath)?.ToString() ?? string.Empty;

        if (string.IsNullOrEmpty(_regDtxPath))
        {
            throw new InvalidProgramException($"Missing registry key \"{RegKeyDtxPath}\".");
        }

        IntPtr hModule = LoadLibrary(Path.Combine(_regDtxPath, DtxAPI));

        if (hModule == IntPtr.Zero)
        {
            throw new InvalidProgramException("Could not load Dentrix.API.dll.");
        }
    }

    private Object? HKLUSoftwareGetValue(string key)
    {
        

        try
        {
            using var subKey = Registry.LocalMachine.OpenSubKey(RegCommonPath);

            var value = subKey?.GetValue(key);

            if (value != null)
            {
                System.Diagnostics.Debugger.Launch();
                return value;
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Registry (original) error at: {time}", DateTimeOffset.Now);
        }
        

        //try
        //{
        //    using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        //    using var subKey = hklm.OpenSubKey(RegCommonPath);

        //    var value = subKey?.GetValue(key);

        //    if (value != null)
        //    {
        //        return value;
        //    }
        //}
        //catch (Exception e)
        //{
        //    _logger.LogError(e, "Registry (original) error at: {time}", DateTimeOffset.Now);
        //}

        //try
        //{
        //    using var hklm = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32);
        //    using var subKey = hklm.OpenSubKey(RegCommonPath);

        //    var value = subKey?.GetValue(key);

        //    if (value != null)
        //    {
        //        return value;
        //    }
        //}
        //catch (Exception e)
        //{
        //    _logger.LogError(e, "Registry (redirected) error at: {time}", DateTimeOffset.Now);
        //}

        return null;
    }

    private const string DtxKey = "MNCN5L2G.dtxkey";
    private const string DtxUser = "MNCN5L2G";
    private const string DtxPassword = "MNCN5L2G5";

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

    public bool IsConnected { get => !string.IsNullOrEmpty(_dbConnStr); }

    /// <summary>
    /// The database connection string returned by Dentrix.
    /// </summary>
    private string _dbConnStr = string.Empty;

    /// <summary>
    /// Ensures only one connection request happens at a time.
    /// </summary>
    private static readonly SemaphoreSlim _semDbConnStr = new(1, 1);

    /// <summary>
    /// Periodically check if the database connection is set.
    /// </summary>
    public void Initialize(CancellationToken stoppingToken)
    {
        _ = Task.Run(async () =>
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ConnectToDatabase(stoppingToken);
                await Task.Delay(10000, stoppingToken);
            }
        }, stoppingToken);
    }

    private async Task ConnectToDatabase(CancellationToken stoppingToken)
    {
        if (!string.IsNullOrEmpty(_dbConnStr))
        {
            // The connection string is already set. Nothing left to do.
            return;
        }

        // IMPORTANT! This is the only place we should acquire this semaphore.
        // If that were to change, this logic also needs to change.
        var locked = await _semDbConnStr.WaitAsync(0, stoppingToken);

        if (!locked)
        {
            // Another connection attempt must already be in progress.
            return;
        }

        try
        {
            var status = DENTRIXAPI_RegisterUser(_regAuthFile);

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
                            RegisterUserErrorMessage(status, _regAuthFile),
                            DateTimeOffset.Now
                        );
                        break;
                    }
            }

#if !DEBUG
            if (status != RU_SUCCESS)
            {
                throw new InvalidProgramException(RegisterUserErrorMessage(status, _regAuthFile));
            }

            var builder = new StringBuilder(512);
            DENTRIXAPI_GetConnectionString(DtxUser, DtxPassword, builder, 512);
            _dbConnStr = builder.ToString();

            if (string.IsNullOrEmpty(_dbConnStr))
            {
                throw new InvalidProgramException("Empty connection string to Dentrix API.");
            }
#endif
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Failed to connect to Dentrix database at: {time}", DateTimeOffset.Now);
        }
        finally
        {
            _semDbConnStr.Release();
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
        if (string.IsNullOrEmpty(_dbConnStr))
        {
            // Can't continue. The server will try the request again.
            yield break;
        }

        using var conn = new OdbcConnection(_dbConnStr);
        try
        {
            await conn.OpenAsync(stoppingToken);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Could not connect to Dentrix database at: {time}", DateTimeOffset.Now);

            // It is possible our background task responsible for determining the connection string
            // updated _dbConnStr in-between when our connection was made and when this exception was
            // thrown. If that was the case, don't throw away our work.
            if (conn.ConnectionString == _dbConnStr)
            {
                _dbConnStr = string.Empty;
            }

            yield break;
        }

        using var command = new OdbcCommand(query, conn);

        OdbcDataReader reader;
        try
        {
            reader = command.ExecuteReader();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Could not execute Dentrix database reader at: {time}", DateTimeOffset.Now);

            yield break;
        }

        using (reader)
        {
            while (true)
            {
                var json = new Dictionary<string, object> { };

                try
                {
                    if (!await reader.ReadAsync(stoppingToken))
                    {
                        yield break;
                    }

                    object[] columns = new object[MAX_COLUMNS];
                    int NumberOfColums = reader.GetValues(columns);

                    for (int i = 0; i < NumberOfColums; i++)
                    {
                        var type = reader.GetFieldType(i);
                        json[reader.GetName(i)] = Convert.ChangeType(columns[i], type);
                    }
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    _logger.LogError(e, "Could not read Dentrix database row at: {time}", DateTimeOffset.Now);

                    yield break;
                }

                yield return json;
            }
        }
    }
}
