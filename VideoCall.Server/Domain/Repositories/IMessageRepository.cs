namespace VideoCall.Server.Domain.Repositories;

// الوصول إلى جدول Messages
public interface IMessageRepository
{
    // يحفظ رسالة واحدة فقط (لا نسخة لكل مستخدم)
    Task<(long MessageId, DateTime SentAt)> AddAsync(int conversationId, int senderId, string content, CancellationToken ct);

    // يعيد الرسائل مرتبة من الأقدم إلى الأحدث؛ beforeMessageId = null يعني أحدث الرسائل
    Task<(IReadOnlyList<ChatMessageRecord> Messages, bool HasMore)> GetPageAsync(
        int conversationId, long? beforeMessageId, int limit, CancellationToken ct);

    // تعديل رسالة يملكها المرسل؛ يعيد وقت التعديل أو null إن لم توجد/ليست له/محذوفة
    Task<DateTime?> EditAsync(long messageId, int conversationId, int senderId, string content, CancellationToken ct);

    // حذف منطقي (IsDeleted = 1) لرسالة يملكها المرسل
    Task<bool> DeleteAsync(long messageId, int conversationId, int senderId, CancellationToken ct);

    // آخر رسالة لكل محادثة من محادثات المستخدم
    Task<IReadOnlyList<ChatMessageRecord>> GetLastMessagesForUserAsync(int userId, CancellationToken ct);
}
