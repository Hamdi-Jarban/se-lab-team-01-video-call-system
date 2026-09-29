using Microsoft.Data.SqlClient;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Persistence;

namespace VideoCall.Server.Infrastructure.Persistence;

public sealed class SqlUserRepository : IUserRepository
{
    private const string SelectColumns =
        "UserId, Username, PasswordHash, DisplayName, CreatedAt, LastLoginAt, IsActive";

    private readonly ISqlConnectionFactory _factory;

    public SqlUserRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<UserRecord?> FindByUsernameAsync(string username, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, $"SELECT {SelectColumns} FROM dbo.Users WHERE Username = @username")
            .With("@username", username);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<UserRecord?> FindByIdAsync(int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, $"SELECT {SelectColumns} FROM dbo.Users WHERE UserId = @id")
            .With("@id", userId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<int?> CreateAsync(string username, string passwordHash, string displayName, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
INSERT INTO dbo.Users (Username, PasswordHash, DisplayName)
OUTPUT INSERTED.UserId
VALUES (@username, @hash, @display)")
            .With("@username", username)
            .With("@hash", passwordHash)
            .With("@display", displayName);

        try
        {
            var result = await command.ExecuteScalarAsync(ct);
            return Convert.ToInt32(result);
        }
        catch (SqlException ex) when (SqlHelpers.IsDuplicateKey(ex))
        {
            return null;
        }
    }

    public async Task UpdateLastLoginAsync(int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, "UPDATE dbo.Users SET LastLoginAt = SYSUTCDATETIME() WHERE UserId = @id")
            .With("@id", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<UserRecord>> FindByUsernamesAsync(IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        if (usernames.Count == 0) return Array.Empty<UserRecord>();

        await using var connection = await _factory.OpenAsync(ct);

        // أسماء المعاملات تُولَّد آليًا (@u0, @u1, ...) والقيم تُمرَّر كمعاملات: لا حقن SQL
        var names = usernames.ToArray();
        var placeholders = string.Join(", ", names.Select((_, i) => $"@u{i}"));
        await using var command = SqlHelpers.Command(connection,
            $"SELECT {SelectColumns} FROM dbo.Users WHERE IsActive = 1 AND Username IN ({placeholders})");
        for (var i = 0; i < names.Length; i++)
            command.With($"@u{i}", names[i]);

        var users = new List<UserRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            users.Add(Map(reader));
        return users;
    }

    private static UserRecord Map(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetString(1),
        r.GetString(2),
        r.GetString(3),
        SqlHelpers.AsUtc(r.GetDateTime(4)),
        r.IsDBNull(5) ? null : SqlHelpers.AsUtc(r.GetDateTime(5)),
        r.GetBoolean(6));
}
