using Microsoft.Data.SqlClient;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Persistence;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Infrastructure.Persistence;

public sealed class SqlChatConversationRepository : IChatConversationRepository
{
    private const string ConversationColumns =
        "c.ConversationId, c.Type, c.Name, c.CreatedByUserId, cu.Username, c.CreatedAt";

    private readonly ISqlConnectionFactory _factory;

    public SqlChatConversationRepository(ISqlConnectionFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<(int ConversationId, bool Created)> GetOrCreatePrivateAsync(int userA, int userB, CancellationToken ct)
    {
        if (userA == userB)
            throw new ArgumentException("A private conversation needs two different users.");

        var key = $"{Math.Min(userA, userB)}:{Math.Max(userA, userB)}";

        await using var connection = await _factory.OpenAsync(ct);

        var existing = await FindPrivateAsync(connection, null, key, ct);
        if (existing is int existingId)
        {
            // إن كان أحد الطرفين قد غادر سابقًا نعيد تفعيل عضويته (المحادثة الخاصة لا تُغادَر عمليًا)
            await ReactivateMemberAsync(connection, null, existingId, userA, ct);
            await ReactivateMemberAsync(connection, null, existingId, userB, ct);
            return (existingId, false);
        }

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);
        try
        {
            int conversationId;
            await using (var insert = SqlHelpers.Command(connection, @"
INSERT INTO dbo.Conversations (Type, Name, CreatedByUserId, PrivateKey)
OUTPUT INSERTED.ConversationId
VALUES (0, NULL, @creator, @key)", transaction)
                .With("@creator", userA)
                .With("@key", key))
            {
                conversationId = Convert.ToInt32(await insert.ExecuteScalarAsync(ct));
            }

            await InsertMemberAsync(connection, transaction, conversationId, userA, ct);
            await InsertMemberAsync(connection, transaction, conversationId, userB, ct);

            await transaction.CommitAsync(ct);
            return (conversationId, true);
        }
        catch (SqlException ex) when (SqlHelpers.IsDuplicateKey(ex))
        {
            // طلب متزامن أنشأ نفس المحادثة قبلنا (UX_Conversations_PrivateKey)
            await transaction.RollbackAsync(CancellationToken.None);
            var winner = await FindPrivateAsync(connection, null, key, ct)
                         ?? throw new InvalidOperationException("Private conversation vanished after a duplicate-key race.");
            return (winner, false);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<int> CreateGroupAsync(string name, int creatorId, IReadOnlyCollection<int> memberIds, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            int conversationId;
            await using (var insert = SqlHelpers.Command(connection, @"
INSERT INTO dbo.Conversations (Type, Name, CreatedByUserId, PrivateKey)
OUTPUT INSERTED.ConversationId
VALUES (1, @name, @creator, NULL)", transaction)
                .With("@name", name)
                .With("@creator", creatorId))
            {
                conversationId = Convert.ToInt32(await insert.ExecuteScalarAsync(ct));
            }

            // المنشئ عضو دائمًا، ثم بقية الأعضاء بدون تكرار
            var all = new HashSet<int> { creatorId };
            foreach (var id in memberIds) all.Add(id);

            foreach (var userId in all)
                await InsertMemberAsync(connection, transaction, conversationId, userId, ct);

            await transaction.CommitAsync(ct);
            return conversationId;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<ChatConversationRecord?> GetAsync(int conversationId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, $@"
SELECT {ConversationColumns}
FROM dbo.Conversations c
JOIN dbo.Users cu ON cu.UserId = c.CreatedByUserId
WHERE c.ConversationId = @id AND c.IsActive = 1")
            .With("@id", conversationId);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? MapConversation(reader) : null;
    }

    public async Task<bool> IsActiveMemberAsync(int conversationId, int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
SELECT COUNT(1)
FROM dbo.ConversationMembers cm
JOIN dbo.Conversations c ON c.ConversationId = cm.ConversationId
WHERE cm.ConversationId = @cid AND cm.UserId = @uid AND cm.IsActive = 1 AND c.IsActive = 1")
            .With("@cid", conversationId)
            .With("@uid", userId);

        return Convert.ToInt32(await command.ExecuteScalarAsync(ct)) > 0;
    }

    public async Task<IReadOnlyList<ChatMemberRecord>> GetActiveMembersAsync(int conversationId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
SELECT cm.ConversationId, u.UserId, u.Username, u.DisplayName
FROM dbo.ConversationMembers cm
JOIN dbo.Users u ON u.UserId = cm.UserId
WHERE cm.ConversationId = @cid AND cm.IsActive = 1
ORDER BY cm.JoinedAt, u.UserId")
            .With("@cid", conversationId);

        return await ReadMembersAsync(command, ct);
    }

    public async Task<IReadOnlyList<int>> AddMembersAsync(int conversationId, IReadOnlyCollection<int> userIds, CancellationToken ct)
    {
        var added = new List<int>();

        await using var connection = await _factory.OpenAsync(ct);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(ct);

        try
        {
            foreach (var userId in userIds.Distinct())
            {
                // عضو سابق غادر: نعيد تفعيله
                if (await ReactivateMemberAsync(connection, transaction, conversationId, userId, ct))
                {
                    added.Add(userId);
                    continue;
                }

                // نستخدم SAVE POINT حتى يبقى تعارض المفتاح (عضو نشط مسبقًا) محصورًا في هذا العضو فقط
                var savePoint = $"sp_{userId}";
                transaction.Save(savePoint);
                try
                {
                    await InsertMemberAsync(connection, transaction, conversationId, userId, ct);
                    added.Add(userId);
                }
                catch (SqlException ex) when (SqlHelpers.IsDuplicateKey(ex))
                {
                    // العضو نشط مسبقًا: لا شيء لفعله
                    transaction.Rollback(savePoint);
                }
            }

            await transaction.CommitAsync(ct);
            return added;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<bool> RemoveMemberAsync(int conversationId, int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
UPDATE dbo.ConversationMembers
SET IsActive = 0, LeftAt = SYSUTCDATETIME()
WHERE ConversationId = @cid AND UserId = @uid AND IsActive = 1")
            .With("@cid", conversationId)
            .With("@uid", userId);

        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    public async Task<IReadOnlyList<ChatConversationRecord>> GetForUserAsync(int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, $@"
SELECT {ConversationColumns}
FROM dbo.Conversations c
JOIN dbo.Users cu ON cu.UserId = c.CreatedByUserId
WHERE c.IsActive = 1
  AND c.ConversationId IN (SELECT cm.ConversationId FROM dbo.ConversationMembers cm WHERE cm.UserId = @uid AND cm.IsActive = 1)
ORDER BY c.ConversationId")
            .With("@uid", userId);

        var rows = new List<ChatConversationRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(MapConversation(reader));
        return rows;
    }

    public async Task<IReadOnlyList<ChatMemberRecord>> GetMembersForUserConversationsAsync(int userId, CancellationToken ct)
    {
        await using var connection = await _factory.OpenAsync(ct);
        await using var command = SqlHelpers.Command(connection, @"
SELECT cm.ConversationId, u.UserId, u.Username, u.DisplayName
FROM dbo.ConversationMembers cm
JOIN dbo.Users u ON u.UserId = cm.UserId
WHERE cm.IsActive = 1
  AND cm.ConversationId IN (SELECT m.ConversationId FROM dbo.ConversationMembers m WHERE m.UserId = @uid AND m.IsActive = 1)
ORDER BY cm.ConversationId, cm.JoinedAt, u.UserId")
            .With("@uid", userId);

        return await ReadMembersAsync(command, ct);
    }

    // ---------- helpers ----------

    private static async Task<int?> FindPrivateAsync(SqlConnection connection, SqlTransaction? transaction, string key, CancellationToken ct)
    {
        await using var command = SqlHelpers
            .Command(connection, "SELECT ConversationId FROM dbo.Conversations WHERE PrivateKey = @key AND IsActive = 1", transaction)
            .With("@key", key);
        var result = await command.ExecuteScalarAsync(ct);
        return result is null or DBNull ? null : Convert.ToInt32(result);
    }

    private static async Task InsertMemberAsync(SqlConnection connection, SqlTransaction transaction, int conversationId, int userId, CancellationToken ct)
    {
        await using var command = SqlHelpers.Command(connection, @"
INSERT INTO dbo.ConversationMembers (ConversationId, UserId)
VALUES (@cid, @uid)", transaction)
            .With("@cid", conversationId)
            .With("@uid", userId);
        await command.ExecuteNonQueryAsync(ct);
    }

    // يعيد true إذا كان هناك سجل عضوية غير نشط تمت إعادة تفعيله
    private static async Task<bool> ReactivateMemberAsync(SqlConnection connection, SqlTransaction? transaction, int conversationId, int userId, CancellationToken ct)
    {
        await using var command = SqlHelpers.Command(connection, @"
UPDATE dbo.ConversationMembers
SET IsActive = 1, LeftAt = NULL, JoinedAt = SYSUTCDATETIME()
WHERE ConversationId = @cid AND UserId = @uid AND IsActive = 0", transaction)
            .With("@cid", conversationId)
            .With("@uid", userId);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static async Task<IReadOnlyList<ChatMemberRecord>> ReadMembersAsync(SqlCommand command, CancellationToken ct)
    {
        var rows = new List<ChatMemberRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            rows.Add(new ChatMemberRecord(reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2), reader.GetString(3)));
        return rows;
    }

    private static ChatConversationRecord MapConversation(SqlDataReader r) => new(
        r.GetInt32(0),
        r.GetByte(1) == 0 ? ConversationType.Private : ConversationType.Group,
        r.IsDBNull(2) ? null : r.GetString(2),
        r.GetInt32(3),
        r.GetString(4),
        SqlHelpers.AsUtc(r.GetDateTime(5)));
}
