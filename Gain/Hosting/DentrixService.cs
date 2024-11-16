using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;

namespace FluidicML.Gain.Hosting;

public sealed class DentrixService
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

    private readonly ILogger<DentrixService> _logger;
    private readonly RegistryService _registryService;

    public DentrixService(
        ILogger<DentrixService> logger,
        RegistryService registryService
    )
    {
        _logger = logger;
        _registryService = registryService;

        IntPtr hModule = LoadLibrary(Path.Combine(registryService.DentrixExePath, DtxAPI));

        if (hModule == IntPtr.Zero)
        {
            throw new InvalidProgramException($"Could not load {DtxAPI}.");
        }
    }

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

    private const string DtxKey = "MNCN5L2G.dtxkey";
    private const string DtxUser = "MNCN5L2G";
    private const string DtxPassword = "MNCN5L2G5";

    /// <summary>
    /// Ensures only one connection request happens at a time.
    /// </summary>
    private static readonly SemaphoreSlim _connectSemaphore = new(1, 1);

    /// <summary>
    /// Attempts to connect to the Dentrix database.
    /// </summary>
    /// <returns>
    /// 1. A null value if another connection request is already occurring.
    /// 2. The connection string otherwise.
    /// </returns>
    public async Task<string?> ConnectAsync()
    {
        // IMPORTANT! This is the only place we should acquire this semaphore.
        var locked = await _connectSemaphore.WaitAsync(0);

        if (!locked)
        {
            return null;
        }

        try
        {
            var status = DENTRIXAPI_RegisterUser(_registryService.AuthKeyFile);

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
                            RegisterUserErrorMessage(status, _registryService.AuthKeyFile),
                            DateTimeOffset.Now
                        );
                        break;
                    }
            }

            if (status != RU_SUCCESS)
            {
                throw new InvalidDataException(
                    RegisterUserErrorMessage(status, _registryService.AuthKeyFile)
                );
            }

            var builder = new StringBuilder(512);
            DENTRIXAPI_GetConnectionString(DtxUser, DtxPassword, builder, 512);
            var connStr = builder.ToString();

            if (string.IsNullOrEmpty(connStr))
            {
                throw new InvalidDataException("Empty connection string to Dentrix API.");
            }

            return connStr;
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e, "Failed to connect to Dentrix database at: {time}", DateTimeOffset.Now);
            throw;
        }
        finally
        {
            _connectSemaphore.Release();
        }
    }
}
