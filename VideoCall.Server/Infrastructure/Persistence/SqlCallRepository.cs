using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Persistence;

namespace VideoCall.Server.Infrastructure.Persistence;

public sealed class SqlCallRepository : ICallRepository
{
    private readonly ISqlConnectionFactory _factory;

    public SqlCallRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task CreateAsync(Guid callId, int? conversationId, string? roomId, int startedByUserId, CallType type, CallStatus status, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"INSERT INTO dbo.Calls (CallId, ConversationId, RoomId, StartedByUserId, CallType, Status)
                                 VALUES (@id, @cid, @room, @uid, @type, @status)")
            .With("@id", callId)
            .With("@cid", conversationId)
            .With("@room", roomId)
            .With("@uid", startedByUserId)
            .With("@type", (byte)type)
            .With("@status", (byte)status);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid callId, CallStatus status, CancellationToken ct)
    {
        var terminal = status is CallStatus.Rejected or CallStatus.Ended or CallStatus.TimedOut or CallStatus.Interrupted;

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
UPDATE dbo.Calls
SET Status = @status,
    EndedAt = CASE WHEN @terminal = 1 THEN SYSUTCDATETIME() ELSE EndedAt END
WHERE CallId = @id AND EndedAt IS NULL")
            .With("@status", (byte)status)
            .With("@terminal", terminal ? 1 : 0)
            .With("@id", callId);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkStaleAsInterruptedAsync(CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
UPDATE dbo.Calls
SET Status = 5, EndedAt = SYSUTCDATETIME()
WHERE EndedAt IS NULL");
        await command.ExecuteNonQueryAsync(ct);
    }
}
