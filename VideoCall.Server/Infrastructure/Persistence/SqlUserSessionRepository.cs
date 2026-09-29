using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Persistence;

namespace VideoCall.Server.Infrastructure.Persistence;

public sealed class SqlUserSessionRepository : IUserSessionRepository
{
    private readonly ISqlConnectionFactory _factory;

    public SqlUserSessionRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task CreateAsync(Guid sessionId, int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, "INSERT INTO dbo.UserSessions (SessionId, UserId) VALUES (@sid, @uid)")
            .With("@sid", sessionId)
            .With("@uid", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task EndAsync(Guid sessionId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
UPDATE dbo.UserSessions
SET IsActive = 0, DisconnectedAt = SYSUTCDATETIME()
WHERE SessionId = @sid AND IsActive = 1")
            .With("@sid", sessionId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task EndAllActiveAsync(CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
UPDATE dbo.UserSessions
SET IsActive = 0, DisconnectedAt = SYSUTCDATETIME()
WHERE IsActive = 1");
        await command.ExecuteNonQueryAsync(ct);
    }
}
