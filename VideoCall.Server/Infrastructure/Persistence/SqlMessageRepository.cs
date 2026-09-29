using Microsoft.Data.SqlClient;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Persistence;

namespace VideoCall.Server.Infrastructure.Persistence;

public sealed class SqlMessageRepository : IMessageRepository
{
    private readonly ISqlConnectionFactory _factory;

    public SqlMessageRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<(long MessageId, DateTime SentAt)> AddAsync(int conversationId, int senderId, string content, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
INSERT INTO dbo.Messages (ConversationId, SenderId, Content)
OUTPUT INSERTED.MessageId, INSERTED.SentAt
VALUES (@cid, @sid, @content)")
            .With("@cid", conversationId)
            .With("@sid", senderId)
            .With("@content", content);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Message insert returned no row.");

        return (reader.GetInt64(0), SqlHelpers.AsUtc(reader.GetDateTime(1)));
    }

    public async Task<(IReadOnlyList<ChatMessageRecord> Messages, bool HasMore)> GetPageAsync(
        int conversationId, long? beforeMessageId, int limit, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 200);

        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
SELECT TOP (@take) m.MessageId, m.ConversationId, m.SenderId, u.Username, u.DisplayName, m.Content, m.SentAt, m.EditedAt
FROM dbo.Messages m
JOIN dbo.Users u ON u.UserId = m.SenderId
WHERE m.ConversationId = @cid
  AND m.IsDeleted = 0
  AND (@before IS NULL OR m.MessageId < @before)
ORDER BY m.MessageId DESC")
            .With("@take", limit + 1)
            .With("@cid", conversationId)
            .With("@before", beforeMessageId);

        var rows = new List<ChatMessageRecord>();
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
                rows.Add(Map(reader));
        }

        var hasMore = rows.Count > limit;
        if (hasMore) rows.RemoveAt(rows.Count - 1);
        rows.Reverse(); // من الأقدم إلى الأحدث
        return (rows, hasMore);
    }

    public async Task<DateTime?> EditAsync(long messageId, int conversationId, int senderId, string content, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
UPDATE dbo.Messages
SET Content = @content, EditedAt = SYSUTCDATETIME()
OUTPUT INSERTED.EditedAt
WHERE MessageId = @id AND ConversationId = @cid AND SenderId = @sid AND IsDeleted = 0")
            .With("@content", content)
            .With("@id", messageId)
            .With("@cid", conversationId)
            .With("@sid", senderId);

        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : SqlHelpers.AsUtc((DateTime)result);
    }

    public async Task<bool> DeleteAsync(long messageId, int conversationId, int senderId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
UPDATE dbo.Messages SET IsDeleted = 1
WHERE MessageId = @id AND ConversationId = @cid AND SenderId = @sid AND IsDeleted = 0")
            .With("@id", messageId)
            .With("@cid", conversationId)
            .With("@sid", senderId);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<ChatMessageRecord>> GetLastMessagesForUserAsync(int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers
            .Command(connection, @"
SELECT x.MessageId, x.ConversationId, x.SenderId, u.Username, u.DisplayName, x.Content, x.SentAt, x.EditedAt
FROM (
    SELECT m.MessageId, m.ConversationId, m.SenderId, m.Content, m.SentAt, m.EditedAt,
           ROW_NUMBER() OVER (PARTITION BY m.ConversationId ORDER BY m.MessageId DESC) AS rn
    FROM dbo.Messages m
    WHERE m.IsDeleted = 0
      AND m.ConversationId IN (SELECT cm.ConversationId FROM dbo.ConversationMembers cm WHERE cm.UserId = @uid AND cm.IsActive = 1)
) x
JOIN dbo.Users u ON u.UserId = x.SenderId
WHERE x.rn = 1")
            .With("@uid", userId);

        var rows = new List<ChatMessageRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(Map(reader));
        return rows;
    }

    private static ChatMessageRecord Map(SqlDataReader r) => new(
        r.GetInt64(0),
        r.GetInt32(1),
        r.GetInt32(2),
        r.GetString(3),
        r.GetString(4),
        r.GetString(5),
        SqlHelpers.AsUtc(r.GetDateTime(6)),
        r.IsDBNull(7) ? null : SqlHelpers.AsUtc(r.GetDateTime(7)));
}
