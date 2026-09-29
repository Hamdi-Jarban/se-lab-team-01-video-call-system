using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Domain.Services;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Application;

public sealed class ChatService : IChatService
{
    private const int DefaultPageSize = 50;
    private const int MaxPreviewLength = 60;

    private readonly IUserRepository _users;
    private readonly IChatConversationRepository _conversations;
    private readonly IMessageRepository _messages;

    public ChatService(
        IUserRepository users,
        IChatConversationRepository conversations,
        IMessageRepository messages)
    {
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _messages = messages ?? throw new ArgumentNullException(nameof(messages));
    }

    public async Task<ChatResult<OpenedConversation>> OpenPrivateAsync(int userId, string otherUsername, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(otherUsername))
            return ChatResult<OpenedConversation>.Fail(ErrorCodes.InvalidRequest);

        var other = await _users.FindByUsernameAsync(otherUsername.Trim(), ct);
        if (other is null || !other.IsActive)
            return ChatResult<OpenedConversation>.Fail(ErrorCodes.UserNotFound, "المستخدم غير موجود.");

        if (other.UserId == userId)
            return ChatResult<OpenedConversation>.Fail(ErrorCodes.InvalidRequest, "لا يمكنك مراسلة نفسك.");

        var (conversationId, created) = await _conversations.GetOrCreatePrivateAsync(userId, other.UserId, ct);
        var opened = await LoadAsync(conversationId, created, ct);
        return opened is null
            ? ChatResult<OpenedConversation>.Fail(ErrorCodes.ConversationNotFound)
            : ChatResult<OpenedConversation>.Ok(opened);
    }

    public async Task<ChatResult<OpenedConversation>> CreateGroupAsync(
        int userId, string name, IReadOnlyCollection<string> memberUsernames, CancellationToken ct)
    {
        var nameError = InputValidation.ValidateGroupName(name);
        if (nameError is not null)
            return ChatResult<OpenedConversation>.Fail(nameError, "اسم المجموعة غير صالح.");

        var resolved = await ResolveUsersAsync(memberUsernames, ct);
        if (resolved.ErrorCode is not null)
            return ChatResult<OpenedConversation>.Fail(resolved.ErrorCode, resolved.ErrorMessage);

        var ids = resolved.Users.Select(u => u.UserId).Where(id => id != userId).ToList();
        if (ids.Count + 1 > InputValidation.MaxGroupMembers)
            return ChatResult<OpenedConversation>.Fail(ErrorCodes.InvalidRequest, "عدد الأعضاء أكبر من الحد المسموح.");

        var conversationId = await _conversations.CreateGroupAsync(name.Trim(), userId, ids, ct);
        var opened = await LoadAsync(conversationId, true, ct);
        return opened is null
            ? ChatResult<OpenedConversation>.Fail(ErrorCodes.ConversationNotFound)
            : ChatResult<OpenedConversation>.Ok(opened);
    }

    public async Task<ChatResult<GroupChange>> AddMembersAsync(
        int userId, int conversationId, IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        var access = await RequireGroupAdminAsync(userId, conversationId, ct);
        if (access.ErrorCode is not null)
            return ChatResult<GroupChange>.Fail(access.ErrorCode, access.ErrorMessage);

        var resolved = await ResolveUsersAsync(usernames, ct);
        if (resolved.ErrorCode is not null)
            return ChatResult<GroupChange>.Fail(resolved.ErrorCode, resolved.ErrorMessage);

        var current = await _conversations.GetActiveMembersAsync(conversationId, ct);
        var newIds = resolved.Users.Select(u => u.UserId).Except(current.Select(m => m.UserId)).ToList();
        if (current.Count + newIds.Count > InputValidation.MaxGroupMembers)
            return ChatResult<GroupChange>.Fail(ErrorCodes.InvalidRequest, "عدد الأعضاء أكبر من الحد المسموح.");

        var addedIds = await _conversations.AddMembersAsync(conversationId, newIds, ct);

        var opened = await LoadAsync(conversationId, false, ct);
        if (opened is null) return ChatResult<GroupChange>.Fail(ErrorCodes.ConversationNotFound);

        var added = opened.Members.Where(m => addedIds.Contains(m.UserId)).ToList();
        return ChatResult<GroupChange>.Ok(new GroupChange(opened, added, null));
    }

    public async Task<ChatResult<GroupChange>> RemoveMemberAsync(
        int userId, int conversationId, string username, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(username))
            return ChatResult<GroupChange>.Fail(ErrorCodes.InvalidRequest);

        var conversation = await _conversations.GetAsync(conversationId, ct);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return ChatResult<GroupChange>.Fail(ErrorCodes.ConversationNotFound);

        var membersBefore = await _conversations.GetActiveMembersAsync(conversationId, ct);
        var requester = membersBefore.FirstOrDefault(m => m.UserId == userId);
        if (requester is null)
            return ChatResult<GroupChange>.Fail(ErrorCodes.NotConversationMember);

        var target = membersBefore.FirstOrDefault(m =>
            m.Username.Equals(username.Trim(), StringComparison.OrdinalIgnoreCase));
        if (target is null)
            return ChatResult<GroupChange>.Fail(ErrorCodes.NotConversationMember, "المستخدم ليس عضوًا في المجموعة.");

        // مدير المجموعة (المنشئ) لا يُزال ولا يغادر؛ بقية الأعضاء يغادرون بأنفسهم، وحده المدير يزيل غيره
        if (target.UserId == conversation.CreatedByUserId)
            return ChatResult<GroupChange>.Fail(ErrorCodes.InvalidRequest, "لا يمكن إزالة مدير المجموعة.");

        if (target.UserId != userId && conversation.CreatedByUserId != userId)
            return ChatResult<GroupChange>.Fail(ErrorCodes.NotGroupAdmin, "فقط مدير المجموعة يستطيع إزالة الأعضاء.");

        if (!await _conversations.RemoveMemberAsync(conversationId, target.UserId, ct))
            return ChatResult<GroupChange>.Fail(ErrorCodes.NotConversationMember);

        var opened = await LoadAsync(conversationId, false, ct);
        if (opened is null) return ChatResult<GroupChange>.Fail(ErrorCodes.ConversationNotFound);

        return ChatResult<GroupChange>.Ok(new GroupChange(opened, Array.Empty<ChatMemberRecord>(), target));
    }

    public async Task<ChatResult<IReadOnlyList<string>>> GetMembersAsync(int userId, int conversationId, CancellationToken ct)
    {
        if (!await _conversations.IsActiveMemberAsync(conversationId, userId, ct))
            return ChatResult<IReadOnlyList<string>>.Fail(ErrorCodes.NotConversationMember);

        var members = await _conversations.GetActiveMembersAsync(conversationId, ct);
        return ChatResult<IReadOnlyList<string>>.Ok(members.Select(m => m.Username).ToList());
    }

    public async Task<ChatResult<SentMessage>> SendMessageAsync(int senderId, int conversationId, string content, CancellationToken ct)
    {
        var contentError = InputValidation.ValidateMessageContent(content);
        if (contentError is not null)
            return ChatResult<SentMessage>.Fail(contentError, "الرسالة فارغة أو طويلة جدًا.");

        // التحقق من وجود المحادثة ثم من عضوية المرسل (SenderId يأتي من الجلسة، لا من العميل)
        if (await _conversations.GetAsync(conversationId, ct) is null)
            return ChatResult<SentMessage>.Fail(ErrorCodes.ConversationNotFound);

        var members = await _conversations.GetActiveMembersAsync(conversationId, ct);
        var sender = members.FirstOrDefault(m => m.UserId == senderId);
        if (sender is null)
            return ChatResult<SentMessage>.Fail(ErrorCodes.NotConversationMember);

        var text = content.Trim();
        var (messageId, sentAt) = await _messages.AddAsync(conversationId, senderId, text, ct);

        var dto = new ChatMessageDto(messageId, conversationId, sender.Username, sender.DisplayName, text, sentAt, null);
        return ChatResult<SentMessage>.Ok(new SentMessage(dto, members));
    }

    public async Task<ChatResult<MessageChange>> EditMessageAsync(int userId, int conversationId, long messageId, string content, CancellationToken ct)
    {
        var contentError = InputValidation.ValidateMessageContent(content);
        if (contentError is not null)
            return ChatResult<MessageChange>.Fail(contentError, "الرسالة فارغة أو طويلة جدًا.");

        var members = await _conversations.GetActiveMembersAsync(conversationId, ct);
        if (members.All(m => m.UserId != userId))
            return ChatResult<MessageChange>.Fail(ErrorCodes.NotConversationMember);

        var text = content.Trim();
        var editedAt = await _messages.EditAsync(messageId, conversationId, userId, text, ct);
        return editedAt is null
            ? ChatResult<MessageChange>.Fail(ErrorCodes.MessageNotFound, "لا يمكنك تعديل هذه الرسالة.")
            : ChatResult<MessageChange>.Ok(new MessageChange(conversationId, messageId, text, editedAt, members));
    }

    public async Task<ChatResult<MessageChange>> DeleteMessageAsync(int userId, int conversationId, long messageId, CancellationToken ct)
    {
        var members = await _conversations.GetActiveMembersAsync(conversationId, ct);
        if (members.All(m => m.UserId != userId))
            return ChatResult<MessageChange>.Fail(ErrorCodes.NotConversationMember);

        return await _messages.DeleteAsync(messageId, conversationId, userId, ct)
            ? ChatResult<MessageChange>.Ok(new MessageChange(conversationId, messageId, null, null, members))
            : ChatResult<MessageChange>.Fail(ErrorCodes.MessageNotFound, "لا يمكنك حذف هذه الرسالة.");
    }

    public async Task<IReadOnlyList<ConversationDto>> GetConversationsAsync(int userId, CancellationToken ct)
    {
        var conversations = await _conversations.GetForUserAsync(userId, ct);
        var members = await _conversations.GetMembersForUserConversationsAsync(userId, ct);
        var lastMessages = (await _messages.GetLastMessagesForUserAsync(userId, ct))
            .ToDictionary(m => m.ConversationId);
        var membersByConversation = members.ToLookup(m => m.ConversationId);

        var result = new List<ConversationDto>(conversations.Count);
        foreach (var conversation in conversations)
        {
            var opened = new OpenedConversation(conversation, membersByConversation[conversation.ConversationId].ToList(), false);
            var dto = opened.ToDto(userId);

            if (lastMessages.TryGetValue(conversation.ConversationId, out var last))
                dto = dto with { LastMessageAtUtc = last.SentAt, LastMessagePreview = Preview(last.Content) };

            result.Add(dto);
        }

        // الأحدث نشاطًا أولًا
        return result
            .OrderByDescending(c => c.LastMessageAtUtc ?? DateTime.MinValue)
            .ThenBy(c => c.ConversationId)
            .ToList();
    }

    public async Task<ChatResult<GetMessagesResponsePayload>> GetMessagesAsync(
        int userId, int conversationId, long? beforeMessageId, int limit, CancellationToken ct)
    {
        if (!await _conversations.IsActiveMemberAsync(conversationId, userId, ct))
            return ChatResult<GetMessagesResponsePayload>.Fail(ErrorCodes.NotConversationMember);

        var take = limit <= 0 ? DefaultPageSize : limit;
        var (rows, hasMore) = await _messages.GetPageAsync(conversationId, beforeMessageId, take, ct);

        var dtos = rows
            .Select(r => new ChatMessageDto(r.MessageId, r.ConversationId, r.SenderUsername, r.SenderDisplayName, r.Content, r.SentAt, r.EditedAt))
            .ToList();

        return ChatResult<GetMessagesResponsePayload>.Ok(new GetMessagesResponsePayload(conversationId, dtos, hasMore));
    }

    // ---------- helpers ----------

    private async Task<OpenedConversation?> LoadAsync(int conversationId, bool created, CancellationToken ct)
    {
        var conversation = await _conversations.GetAsync(conversationId, ct);
        if (conversation is null) return null;

        var members = await _conversations.GetActiveMembersAsync(conversationId, ct);
        return new OpenedConversation(conversation, members, created);
    }

    private async Task<(string? ErrorCode, string? ErrorMessage)> RequireGroupAdminAsync(int userId, int conversationId, CancellationToken ct)
    {
        var conversation = await _conversations.GetAsync(conversationId, ct);
        if (conversation is null || conversation.Type != ConversationType.Group)
            return (ErrorCodes.ConversationNotFound, null);

        if (!await _conversations.IsActiveMemberAsync(conversationId, userId, ct))
            return (ErrorCodes.NotConversationMember, null);

        if (conversation.CreatedByUserId != userId)
            return (ErrorCodes.NotGroupAdmin, "فقط مدير المجموعة يستطيع إضافة الأعضاء.");

        return (null, null);
    }

    private async Task<(IReadOnlyList<UserRecord> Users, string? ErrorCode, string? ErrorMessage)> ResolveUsersAsync(
        IReadOnlyCollection<string> usernames, CancellationToken ct)
    {
        var wanted = (usernames ?? Array.Empty<string>())
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (wanted.Count > InputValidation.MaxGroupMembers)
            return (Array.Empty<UserRecord>(), ErrorCodes.InvalidRequest, "عدد الأعضاء أكبر من الحد المسموح.");

        var found = await _users.FindByUsernamesAsync(wanted, ct);
        var missing = wanted
            .Where(w => !found.Any(f => f.Username.Equals(w, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count > 0)
            return (Array.Empty<UserRecord>(), ErrorCodes.UserNotFound, $"مستخدمون غير موجودين: {string.Join("، ", missing)}");

        return (found, null, null);
    }

    private static string Preview(string content)
    {
        var singleLine = content.Replace('\r', ' ').Replace('\n', ' ');
        return singleLine.Length <= MaxPreviewLength ? singleLine : singleLine[..MaxPreviewLength] + "…";
    }
}
