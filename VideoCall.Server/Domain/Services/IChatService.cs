using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Domain.Services;

public sealed record ChatResult<T>(T? Value, string? ErrorCode, string? ErrorMessage = null)
{
    public bool Success => ErrorCode is null;

    public static ChatResult<T> Ok(T value) => new(value, null);

    public static ChatResult<T> Fail(string code, string? message = null) => new(default, code, message);
}

// محادثة مع أعضائها (تُحوَّل إلى DTO بحسب المستخدم الذي سيراها)
public sealed record OpenedConversation(
    ChatConversationRecord Conversation,
    IReadOnlyList<ChatMemberRecord> Members,
    bool Created)
{
    public ConversationDto ToDto(int viewerUserId)
    {
        var name = Conversation.Type == ConversationType.Private
            ? Members.FirstOrDefault(m => m.UserId != viewerUserId)?.Username ?? "?"
            : Conversation.Name ?? string.Empty;

        return new ConversationDto(
            Conversation.ConversationId,
            Conversation.Type,
            name,
            Conversation.CreatedByUsername,
            Members.Select(m => m.Username).ToList(),
            null,
            null);
    }
}

public sealed record GroupChange(
    OpenedConversation Conversation,
    IReadOnlyList<ChatMemberRecord> Added,
    ChatMemberRecord? Removed);

// نتيجة تعديل/حذف رسالة (Content و EditedAt فارغان في الحذف)
public sealed record MessageChange(int ConversationId, long MessageId, string? Content, DateTime? EditedAt, IReadOnlyList<ChatMemberRecord> Members);

public sealed record SentMessage(ChatMessageDto Message, IReadOnlyList<ChatMemberRecord> Members);

// منطق المراسلة الدائمة (تحقق الصلاحيات + التخزين). لا يعرف شيئًا عن TCP.
public interface IChatService
{
    Task<ChatResult<OpenedConversation>> OpenPrivateAsync(int userId, string otherUsername, CancellationToken ct);

    Task<ChatResult<OpenedConversation>> CreateGroupAsync(int userId, string name, IReadOnlyCollection<string> memberUsernames, CancellationToken ct);

    Task<ChatResult<GroupChange>> AddMembersAsync(int userId, int conversationId, IReadOnlyCollection<string> usernames, CancellationToken ct);

    Task<ChatResult<GroupChange>> RemoveMemberAsync(int userId, int conversationId, string username, CancellationToken ct);

    Task<ChatResult<IReadOnlyList<string>>> GetMembersAsync(int userId, int conversationId, CancellationToken ct);

    Task<ChatResult<SentMessage>> SendMessageAsync(int senderId, int conversationId, string content, CancellationToken ct);

    Task<ChatResult<MessageChange>> EditMessageAsync(int userId, int conversationId, long messageId, string content, CancellationToken ct);

    Task<ChatResult<MessageChange>> DeleteMessageAsync(int userId, int conversationId, long messageId, CancellationToken ct);

    Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(int userId, CancellationToken ct);

    Task<ChatResult<GetMessagesResponsePayload>> GetMessagesAsync(int userId, int conversationId, long? beforeMessageId, int limit, CancellationToken ct);
}
