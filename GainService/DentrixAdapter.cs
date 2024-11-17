using System.Data.Odbc;
using System.IO.IsolatedStorage;
using System.Runtime.CompilerServices;

namespace FluidicML.Gain;

public enum QueryResultStatus
{
    // A row was successfully retrieved from the database.
    RESULT = 0,
    // We successfully finished returning all rows of a query.
    FINISHED = 1,
    // The Dentrix database connection string is not set.
    DISCONNECTED = 2,
    // A connection string exists but could not be used.
    CONNECT_FAILED = 3,
    // An invalid query was given to the Dentrix database.
    INVALID_QUERY = 4,
    // An unexpected error occurred mid-query.
    INTERRUPTED = 5,
}

public sealed class QueryResult
{
    public required QueryResultStatus status;
    public required IDictionary<string, object>? value;
}

public sealed class DentrixAdapter
{
    private readonly ILogger<DentrixAdapter> _logger;
    private readonly IConfiguration _config;

    private string _databaseConnStr;
    public bool IsConnected
    {
        get => !string.IsNullOrEmpty(_databaseConnStr);
    }

    public DentrixAdapter(ILogger<DentrixAdapter> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;

        _databaseConnStr = ReadIsolatedStorage();
    }

    private string ReadIsolatedStorage()
    {
        var fileName = _config.GetValue<string>("Storage:DentrixFile")!;

        using IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();

        try
        {
            if (storage.FileExists(fileName))
            {
                using IsolatedStorageFileStream stream = storage.OpenFile(fileName, FileMode.Open, FileAccess.Read);
                using StreamReader reader = new(stream);

                var encoded = reader.ReadLine();
                if (encoded == null)
                {
                    return string.Empty;
                }
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not read {fileName} at: {time}", fileName, DateTimeOffset.Now);
        }

        return string.Empty;
    }

    private void WriteIsolatedStorage()
    {
        var fileName = _config.GetValue<string>("Storage:DentrixFile")!;

        using IsolatedStorageFile storage = IsolatedStorageFile.GetUserStoreForDomain();
        using IsolatedStorageFileStream stream = storage.OpenFile(fileName, FileMode.Create, FileAccess.Write);
        using StreamWriter writer = new(stream);

        var encoded = System.Text.Encoding.UTF8.GetBytes(_databaseConnStr);
        writer.WriteLine(Convert.ToBase64String(encoded));
        writer.Flush();
    }

    public void Connect(string databaseConnStr)
    {
        _databaseConnStr = databaseConnStr;
        WriteIsolatedStorage();
    }

    private const int MAX_COLUMNS = 256;

    public async IAsyncEnumerable<QueryResult> Query(
        string query,
        [EnumeratorCancellation] CancellationToken stoppingToken
    )
    {
        if (string.IsNullOrEmpty(_databaseConnStr))
        {
            _logger.LogError("Query made without Dentrix connection at: {time}", DateTimeOffset.Now);
            yield return new() { status = QueryResultStatus.DISCONNECTED, value = null };
            yield break;
        }

        OdbcConnection? conn = new(_databaseConnStr);
        try
        {
            await conn.OpenAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            conn.Dispose();
            conn = null;
            throw;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Could not connect to Dentrix at: {time}", DateTimeOffset.Now);

            // It is possible the connection string changed between when our connection was made
            // and when this exception threw. If that was the case, don't reset our work. Otherwise
            // we assume our connection string is no longer valid. We can resume once our frontend
            // application gives us a new one.
            if (conn.ConnectionString == _databaseConnStr)
            {
                Connect(string.Empty);
            }

            conn.Dispose();
            conn = null;
        }

        if (conn == null)
        {
            yield return new() { status = QueryResultStatus.CONNECT_FAILED, value = null };
            yield break;
        }

        using (conn)
        {
            using var command = new OdbcCommand(query, conn);

            OdbcDataReader? reader = null;
            try
            {
                reader = command.ExecuteReader();
            }
            catch (OperationCanceledException)
            {
                reader?.Dispose();
                reader = null;
                throw;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Could not execute Dentrix database reader at: {time}", DateTimeOffset.Now);
                reader?.Dispose();
                reader = null;
            }

            if (reader == null)
            {
                yield return new() { status = QueryResultStatus.INVALID_QUERY, value = null };
                yield break;
            }

            using (reader)
            {
                while (true)
                {
                    var reading = true;
                    Dictionary<string, object>? json = [];
                    OperationCanceledException? canceled = null;

                    try
                    {
                        reading = await reader.ReadAsync(stoppingToken);

                        if (reading)
                        {
                            object[] columns = new object[MAX_COLUMNS];
                            int numberOfColumns = reader.GetValues(columns);

                            for (int i = 0; i < numberOfColumns; i++)
                            {
                                var type = reader.GetFieldType(i);
                                json[reader.GetName(i)] = Convert.ChangeType(columns[i], type);
                            }
                        }
                    }
                    catch (OperationCanceledException e)
                    {
                        _logger.LogError(e, "Dentrix reader interrupted at: {time}", DateTimeOffset.Now);
                        canceled = e;
                    }
                    catch (Exception e)
                    {
                        _logger.LogError(e, "Dentrix reader interrupted at: {time}", DateTimeOffset.Now);
                        json = null;
                    }

                    if (!reading)
                    {
                        yield return new() { status = QueryResultStatus.FINISHED, value = null };
                        yield break;
                    }

                    if (canceled == null && json != null)
                    {
                        yield return new() { status = QueryResultStatus.RESULT, value = json };
                        continue;
                    }

                    yield return new() { status = QueryResultStatus.INTERRUPTED, value = null };

                    if (canceled != null)
                    {
                        throw canceled;
                    }

                    yield break;
                }
            }
        }
    }
}
