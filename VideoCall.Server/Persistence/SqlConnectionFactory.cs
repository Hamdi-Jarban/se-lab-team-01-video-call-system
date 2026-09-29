using Microsoft.Data.SqlClient;

namespace VideoCall.Server.Persistence;

// مصنع الاتصالات: كل عملية تفتح اتصالًا قصير العمر (Connection Pooling يتكفل بالأداء)
public interface ISqlConnectionFactory
{
    Task<SqlConnection> OpenAsync(CancellationToken ct);
}

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(DatabaseOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _connectionString = options.ConnectionString;
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new SqlConnection(_connectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}

// دوال مساعدة صغيرة لتقليل التكرار في ADO.NET
internal static class SqlHelpers
{
    public static SqlCommand Command(SqlConnection connection, string sql, SqlTransaction? transaction = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        return command;
    }

    public static SqlCommand With(this SqlCommand command, string name, object? value)
    {
        command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return command;
    }

    // انتهاك قيد Unique / Primary Key
    public static bool IsDuplicateKey(SqlException ex) => ex.Number is 2601 or 2627;

    public static DateTime AsUtc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
