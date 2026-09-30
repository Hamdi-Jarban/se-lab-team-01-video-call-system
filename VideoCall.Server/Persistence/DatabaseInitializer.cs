using Microsoft.Data.SqlClient;
using VideoCall.Server.Domain.Logging;

namespace VideoCall.Server.Persistence;

// تهيئة قاعدة البيانات عند بدء الخادم:
// LocalDB -> إنشاء القاعدة إذا لزم -> إنشاء الجداول والقيود والفهارس إذا لزم.
public sealed class DatabaseInitializer
{
    private const int MaxAttempts = 5;

    private readonly string _connectionString;
    private readonly IAppLogger _logger;

    public DatabaseInitializer(DatabaseOptions options, IAppLogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.ConnectionString;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InitializeAsync(CancellationToken ct)
    {
        var target = new SqlConnectionStringBuilder(_connectionString);
        var databaseName = target.InitialCatalog;

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new InvalidOperationException("The connection string must specify a database name (Database=...).");

        var master = new SqlConnectionStringBuilder(_connectionString) { InitialCatalog = "master" };

        Exception? last = null;
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                await EnsureDatabaseAsync(master.ConnectionString, databaseName, ct);
                await EnsureSchemaAsync(ct);
                _logger.Info($"Database '{databaseName}' is ready.");
                return;
            }
            catch (SqlException ex) when (attempt < MaxAttempts)
            {
                // أول اتصال بـ LocalDB قد يحتاج بضع ثوانٍ لتشغيل النسخة
                last = ex;
                _logger.Warn($"SQL Server is not ready (attempt {attempt}/{MaxAttempts}): {ex.Message}");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }

        throw new InvalidOperationException("Could not initialize the SQL Server database.", last);
    }

    private static async Task EnsureDatabaseAsync(string masterConnectionString, string databaseName, CancellationToken ct)
    {
        await using var connection = new SqlConnection(masterConnectionString);
        await connection.OpenAsync(ct);

        // اسم القاعدة يُمرَّر كمعامل ويُقتبس بـ QUOTENAME لمنع أي حقن SQL
        const string sql = @"
IF DB_ID(@name) IS NULL
BEGIN
    DECLARE @statement NVARCHAR(MAX) = N'CREATE DATABASE ' + QUOTENAME(@name);
    EXEC (@statement);
END";

        await using var command = SqlHelpers.Command(connection, sql).With("@name", databaseName);
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task EnsureSchemaAsync(CancellationToken ct)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            foreach (var statement in DatabaseSchema.Statements)
            {
                await using var command = SqlHelpers.Command(connection, statement, transaction);
                await command.ExecuteNonQueryAsync(ct);
            }

            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
