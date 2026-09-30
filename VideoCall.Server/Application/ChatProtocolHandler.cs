using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Domain.Services;
using VideoCall.Shared.Messages;

namespace VideoCall.Server.Application;

// يعالج رسائل البروتوكول الخاصة بالمراسلة الدائمة ويفوّض المنطق إلى IChatService.
// الهوية (UserId) تُؤخذ دائمًا من الجلسة المصادَق عليها وليس من محتوى الرسالة.
public sealed class ChatProtocolHandler
{
    private readonly IChatService _chat;
    private readonly IUserPresenceRepository _presence;
    private readonly IAppLogger _logger;

    public ChatProtocolHandler(IChatService chat, IUserPresenceRepository presence, IAppLogger logger)
    {
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public static bool CanHandle(MessageType type) => type is
        MessageType.OpenPrivateChatRequest or
        MessageType.CreateGroupRequest or
        MessageType.AddGroupMembersRequest or
        MessageType.RemoveGroupMemberRequest or
        MessageType.GetGroupMembersRequest or
        MessageType.SendMessageRequest or
        MessageType.EditMessageRequest or
        MessageType.DeleteMessageRequest or
        MessageType.GetConversationsRequest or
        MessageType.GetMessagesRequest;

    public async Task HandleAsync(IClientHandler session, Message message, CancellationToken ct)
    {
        if (!session.IsAuthenticated || session.UserId <= 0 || session.Username is null)
        {
            await SendErrorAsync(session, ErrorCodes.NotAuthenticated, "يجب تسجيل الدخول أولًا.", ct);
            return;
        }

        try
        {
            switch (message.Type)
            {
                case MessageType.OpenPrivateChatRequest:
                    await OpenPrivateAsync(session, message.ReadPayload<OpenPrivateChatRequestPayload>(), ct);
                    break;
                case MessageType.CreateGroupRequest:
                    await CreateGroupAsync(session, message.ReadPayload<CreateGroupRequestPayload>(), ct);
                    break;
                case MessageType.AddGroupMembersRequest:
                    await AddMembersAsync(session, message.ReadPayload<AddGroupMembersRequestPayload>(), ct);
                    break;
                case MessageType.RemoveGroupMemberRequest:
                    await RemoveMemberAsync(session, message.ReadPayload<RemoveGroupMemberRequestPayload>(), ct);
                    break;
                case MessageType.GetGroupMembersRequest:
                    await GetMembersAsync(session, message.ReadPayload<GetGroupMembersRequestPayload>(), ct);
                    break;
                case MessageType.SendMessageRequest:
                    await SendMessageAsync(session, message.ReadPayload<SendMessageRequestPayload>(), ct);
                    break;
                case MessageType.EditMessageRequest:
                    await EditMessageAsync(session, message.ReadPayload<EditMessageRequestPayload>(), ct);
                    break;
                case MessageType.DeleteMessageRequest:
                    await DeleteMessageAsync(session, message.ReadPayload<DeleteMessageRequestPayload>(), ct);
                    break;
                case MessageType.GetConversationsRequest:
                    await GetConversationsAsync(session, ct);
                    break;
                case MessageType.GetMessagesRequest:
                    await GetMessagesAsync(session, message.ReadPayload<GetMessagesRequestPayload>(), ct);
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // أي خطأ SQL/غير متوقع لا يجب أن يسقط جلسة TCP بأكملها (ولا المكالمات الجارية)
            _logger.Error($"Chat request {message.Type} from {session.Username} failed: {ex}");
            await SendErrorAsync(session, ErrorCodes.DatabaseUnavailable, "تعذر تنفيذ الطلب حاليًا، حاول لاحقًا.", ct);
        }
    }

    private async Task OpenPrivateAsync(IClientHandler session, OpenPrivateChatRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.OpenPrivateAsync(session.UserId, request.Username, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var opened = result.Value;
        await SendAsync(session, Message.Create(MessageType.ConversationOpened,
            new ConversationOpenedPayload(opened.ToDto(session.UserId), session.Username!)), ct);

        // الطرف الآخر يرى المحادثة في قائمته فور إنشائها (لأول مرة فقط)
        if (opened.Created)
        {
            foreach (var member in opened.Members.Where(m => m.UserId != session.UserId))
            {
                await SendToUserAsync(member.Username, Message.Create(MessageType.ConversationOpened,
                    new ConversationOpenedPayload(opened.ToDto(member.UserId), session.Username!)), ct);
            }
        }
    }

    private async Task CreateGroupAsync(IClientHandler session, CreateGroupRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.CreateGroupAsync(session.UserId, request.Name, request.MemberUsernames ?? new List<string>(), ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var opened = result.Value;
        foreach (var member in opened.Members)
        {
            await SendToUserAsync(member.Username, Message.Create(MessageType.ConversationOpened,
                new ConversationOpenedPayload(opened.ToDto(member.UserId), session.Username!)), ct);
        }
    }

    private async Task AddMembersAsync(IClientHandler session, AddGroupMembersRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.AddMembersAsync(session.UserId, request.ConversationId, request.Usernames ?? new List<string>(), ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var change = result.Value;
        var addedIds = change.Added.Select(a => a.UserId).ToHashSet();

        foreach (var member in change.Conversation.Members)
        {
            var message = addedIds.Contains(member.UserId)
                ? Message.Create(MessageType.ConversationOpened,
                    new ConversationOpenedPayload(change.Conversation.ToDto(member.UserId), session.Username!))
                : Message.Create(MessageType.ConversationMembersUpdate,
                    new ConversationMembersPayload(request.ConversationId, change.Conversation.Members.Select(m => m.Username).ToList()));

            await SendToUserAsync(member.Username, message, ct);
        }
    }

    private async Task RemoveMemberAsync(IClientHandler session, RemoveGroupMemberRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.RemoveMemberAsync(session.UserId, request.ConversationId, request.Username, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var change = result.Value;
        if (change.Removed is not null)
        {
            await SendToUserAsync(change.Removed.Username, Message.Create(MessageType.ConversationRemoved,
                new ConversationRemovedPayload(request.ConversationId)), ct);
        }

        var update = Message.Create(MessageType.ConversationMembersUpdate,
            new ConversationMembersPayload(request.ConversationId, change.Conversation.Members.Select(m => m.Username).ToList()));
        foreach (var member in change.Conversation.Members)
            await SendToUserAsync(member.Username, update, ct);
    }

    private async Task GetMembersAsync(IClientHandler session, GetGroupMembersRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.GetMembersAsync(session.UserId, request.ConversationId, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        await SendAsync(session, Message.Create(MessageType.ConversationMembersUpdate,
            new ConversationMembersPayload(request.ConversationId, result.Value.ToList())), ct);
    }

    private async Task SendMessageAsync(IClientHandler session, SendMessageRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        // SenderId = session.UserId (لا نثق بأي معرّف مرسل من العميل)
        var result = await _chat.SendMessageAsync(session.UserId, request.ConversationId, request.Content, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        // رسالة واحدة محفوظة في SQL ثم توزَّع على كل الأعضاء المتصلين (بمن فيهم المرسل كتأكيد حفظ)
        var broadcast = Message.Create(MessageType.MessageReceived, new MessageReceivedPayload(result.Value.Message));
        foreach (var member in result.Value.Members)
            await SendToUserAsync(member.Username, broadcast, ct);
    }

    private async Task EditMessageAsync(IClientHandler session, EditMessageRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.EditMessageAsync(session.UserId, request.ConversationId, request.MessageId, request.Content, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var change = result.Value;
        var broadcast = Message.Create(MessageType.MessageEdited,
            new MessageEditedPayload(change.ConversationId, change.MessageId, change.Content ?? string.Empty, change.EditedAt ?? DateTime.UtcNow));
        foreach (var member in change.Members)
            await SendToUserAsync(member.Username, broadcast, ct);
    }

    private async Task DeleteMessageAsync(IClientHandler session, DeleteMessageRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.DeleteMessageAsync(session.UserId, request.ConversationId, request.MessageId, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        var change = result.Value;
        var broadcast = Message.Create(MessageType.MessageDeleted,
            new MessageDeletedPayload(change.ConversationId, change.MessageId));
        foreach (var member in change.Members)
            await SendToUserAsync(member.Username, broadcast, ct);
    }

    private async Task GetConversationsAsync(IClientHandler session, CancellationToken ct)
    {
        var conversations = await _chat.GetConversationsAsync(session.UserId, ct);
        await SendAsync(session, Message.Create(MessageType.GetConversationsResponse,
            new GetConversationsResponsePayload(conversations.ToList())), ct);
    }

    private async Task GetMessagesAsync(IClientHandler session, GetMessagesRequestPayload? request, CancellationToken ct)
    {
        if (request is null) { await SendErrorAsync(session, ErrorCodes.InvalidRequest, "طلب غير صالح.", ct); return; }

        var result = await _chat.GetMessagesAsync(session.UserId, request.ConversationId, request.BeforeMessageId, request.Limit, ct);
        if (!result.Success || result.Value is null)
        {
            await SendErrorAsync(session, result.ErrorCode!, result.ErrorMessage, ct);
            return;
        }

        await SendAsync(session, Message.Create(MessageType.GetMessagesResponse, result.Value), ct);
    }

    private async Task SendToUserAsync(string username, Message message, CancellationToken ct)
    {
        if (!_presence.TryGet(username, out var target)) return;
        await SendAsync(target, message, ct);
    }

    // فشل الإرسال لمستخدم واحد (انقطع اتصاله) لا يجب أن يمنع بقية الأعضاء من استلام الرسالة
    private async Task SendAsync(IClientHandler target, Message message, CancellationToken ct)
    {
        try
        {
            await target.SendAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not deliver {message.Type} to {target.Username ?? "unknown"}: {ex.Message}");
        }
    }

    private Task SendErrorAsync(IClientHandler session, string code, string? text, CancellationToken ct) =>
        SendAsync(session, Message.Create(MessageType.ChatError,
            new ChatErrorPayload(code, text ?? code)), ct);
}
