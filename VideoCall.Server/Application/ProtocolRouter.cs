using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Messages;
using System.Collections.Concurrent;
using VideoCall.Shared.Models;

namespace VideoCall.Server.Application;

/// <summary>
/// ÌÊÃ¯Â —”«∆· »—Ê ÊﬂÊ· TCP ≈·Ï «·⁄„·Ì«  «·„‰«”»… œ«Œ· «· ÿ»Ìﬁ°
/// „À·  ”ÃÌ· «·œŒÊ· Ê«·„ﬂ«·„«  Ê≈œ«—… «·€—›.
/// Ì⁄ „œ ⁄·Ï Ê«ÃÂ«  Domain Ê·« Ì ⁄«„· „⁄ Socket „»«‘—….
/// </summary>
public sealed class ProtocolRouter : IProtocolMessageDispatcher, IConnectionLifecycleHandler
{
    private readonly IUserPresenceRepository _presence;
    private readonly IConversationRepository _conversations;
    private readonly IMediaRelayCoordinator _media;
    private readonly ICredentialValidator _credentials;
    private readonly IAppLogger _logger;
    private readonly ConcurrentDictionary<Guid, PendingRoomInvite> _pendingInvites = new();

    private sealed record PendingRoomInvite(Guid InviteId, string RoomId, string Host, string Invitee);

    public ProtocolRouter(
        IUserPresenceRepository presence,
        IConversationRepository conversations,
        IMediaRelayCoordinator media,
        ICredentialValidator credentials,
        IAppLogger logger)
    {
        _presence = presence ?? throw new ArgumentNullException(nameof(presence));
        _conversations = conversations ?? throw new ArgumentNullException(nameof(conversations));
        _media = media ?? throw new ArgumentNullException(nameof(media));
        _credentials = credentials ?? throw new ArgumentNullException(nameof(credentials));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task DispatchAsync(IClientHandler session, Message message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(message);

        switch (message.Type)
        {
            case MessageType.LoginRequest:
                await LoginAsync(session, message.ReadPayload<LoginRequestPayload>(), ct);
                return;

            case MessageType.CallRequest:
                await RequestPrivateCallAsync(session, message.ReadPayload<CallRequestPayload>(), ct);
                return;

            case MessageType.CallAccepted:
                await AcceptPrivateCallAsync(session, message.ReadPayload<CallAcceptedPayload>(), ct);
                return;

            case MessageType.CallRejected:
                await RejectPrivateCallAsync(session, message.ReadPayload<CallRejectedPayload>(), ct);
                return;

            case MessageType.CallEnded:
                await EndPrivateCallAsync(session, message.ReadPayload<CallEndedPayload>(), ct);
                return;

            case MessageType.CreateRoomRequest:
                await CreateRoomAsync(session, message.ReadPayload<CreateRoomRequestPayload>(), ct);
                return;

            case MessageType.AddUserToRoomRequest:
                await AddUserToRoomAsync(session, message.ReadPayload<AddUserToRoomRequestPayload>(), ct);
                return;

            case MessageType.RoomInviteAccepted:
                await AcceptRoomInviteAsync(session, message.ReadPayload<RoomInviteAcceptedPayload>(), ct);
                return;

            case MessageType.RoomInviteRejected:
                await RejectRoomInviteAsync(session, message.ReadPayload<RoomInviteRejectedPayload>(), ct);
                return;

            case MessageType.JoinRoomRequest:
                await JoinRoomAsync(session, message.ReadPayload<JoinRoomRequestPayload>(), ct);
                return;

            case MessageType.LeaveRoomRequest:
                await LeaveConversationAsync(session, message.ReadPayload<LeaveRoomRequestPayload>(), ct);
                return;

            case MessageType.StartRoomMedia:
                await StartGroupMediaAsync(session, message.ReadPayload<StartRoomMediaPayload>(), ct);
                return;

            case MessageType.StopRoomMedia:
                await StopGroupMediaAsync(session, message.ReadPayload<StopRoomMediaPayload>(), ct);
                return;

            case MessageType.Disconnect:
                await session.CloseAsync();
                return;

            default:
                await SendErrorAsync(session, ErrorCodes.UnexpectedError, "Unsupported message type.", ct);
                return;
        }
    }

    private async Task LoginAsync(IClientHandler session, LoginRequestPayload? request, CancellationToken ct)
    {
        if (request is null || session.IsAuthenticated)
        {
            await SendErrorAsync(session, ErrorCodes.InvalidCredentials, "Invalid login request.", ct);
            return;
        }

        var username = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(username) || username.Length > 64 ||
            !_credentials.Validate(username, request.Password ?? string.Empty))
        {
            await session.SendAsync(Message.Create(
                MessageType.LoginResponse,
                new LoginResponsePayload(false, ErrorCodes.InvalidCredentials, null, null)), ct);
            return;
        }

        if (!_presence.TryAdd(username, session))
        {
            await session.SendAsync(Message.Create(
                MessageType.LoginResponse,
                new LoginResponsePayload(false, ErrorCodes.AlreadyLoggedIn, null, null)), ct);
            return;
        }

        session.SetAuthenticatedUsername(username);
        _logger.Info($"{username} logged in.");
        await session.SendAsync(Message.Create(
            MessageType.LoginResponse,
            new LoginResponsePayload(true, null, username, session.SessionToken)), ct);

        await BroadcastPresenceAsync(ct);
    }

    private async Task RequestPrivateCallAsync(IClientHandler session, CallRequestPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var caller) || request is null) return;
        var callee = request.Callee?.Trim();
        if (string.IsNullOrWhiteSpace(callee) || caller.Equals(callee, StringComparison.OrdinalIgnoreCase)) return;
        if (!_presence.IsOnline(callee))
        {
            await SendErrorAsync(session, ErrorCodes.TargetOffline, "The target user is offline.", ct);
            return;
        }

        var callId = Guid.NewGuid();
        var result = _conversations.CreatePrivate(callId.ToString("N"), caller, callee, out _);
        if (result != ConversationOperation.Success)
        {
            await SendErrorAsync(session, ErrorCodes.TargetBusy, "The private conversation could not be created.", ct);
            return;
        }

        var callRequestMessage = Message.Create(
            MessageType.CallRequest,
            new CallRequestPayload(callId, caller, callee));
        await SendToUserAsync(callee, callRequestMessage, ct);
        // ≈⁄«œ… „⁄—› «·„ﬂ«·„… «·–Ì √‰‘√Â «·Œ«œ„ ≈·Ï «·⁄„Ì·.
        await SendToUserAsync(caller, callRequestMessage, ct);
    }

    private async Task AcceptPrivateCallAsync(IClientHandler session, CallAcceptedPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var id = request.CallId.ToString("N");
        var result = _conversations.ActivateMedia(id, username, out _);
        if (result != ConversationOperation.Success)
        {
            await SendErrorAsync(session, ErrorCodes.InvalidCallState, "The call cannot be accepted.", ct);
            return;
        }

        await SendToUserAsync(request.Caller, Message.Create(
            MessageType.CallAccepted,
            new CallAcceptedPayload(request.CallId, request.Caller, username)), ct);
        await StartMediaForConversationAsync(id, ct);
    }

    private async Task RejectPrivateCallAsync(IClientHandler session, CallRejectedPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var id = request.CallId.ToString("N");
        var result = _conversations.EndPrivate(id, username, out var members);
        if (result != ConversationOperation.Success) return;

        _media.ForgetConversation(id);
        var message = Message.Create(
            MessageType.CallRejected,
            new CallRejectedPayload(request.CallId, request.Caller, username));
        foreach (var member in members.Where(x => !x.Equals(username, StringComparison.OrdinalIgnoreCase)))
            await SendToUserAsync(member, message, ct);
    }

    private async Task EndPrivateCallAsync(IClientHandler session, CallEndedPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var id = request.CallId.ToString("N");
        var result = _conversations.EndPrivate(id, username, out var members);
        if (result != ConversationOperation.Success) return;

        _media.ForgetConversation(id);
        var message = Message.Create(MessageType.CallEnded, new CallEndedPayload(request.CallId, username));
        foreach (var member in members.Where(x => !x.Equals(username, StringComparison.OrdinalIgnoreCase)))
            await SendToUserAsync(member, message, ct);
    }

    private async Task CreateRoomAsync(IClientHandler session, CreateRoomRequestPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var id = request.RoomId?.Trim();
        var result = _conversations.CreateGroup(id ?? string.Empty, username, out var conversation);
        if (result != ConversationOperation.Success || conversation is null)
        {
            await SendRoomErrorAsync(session, result, ct);
            return;
        }

        await BroadcastConversationUpdateAsync(conversation, ct);
    }

    private async Task AddUserToRoomAsync(IClientHandler session, AddUserToRoomRequestPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var host) || request is null) return;
        var invitee = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(invitee) || !_presence.IsOnline(invitee))
        {
            await SendErrorAsync(session, ErrorCodes.UserNotFound, "The invited user is offline.", ct);
            return;
        }
        if (!_conversations.TryGet(request.RoomId, out var room) || room is null ||
            !room.Host.Equals(host, StringComparison.OrdinalIgnoreCase))
        {
            await SendRoomErrorAsync(session, ConversationOperation.NotHost, ct);
            return;
        }
        var inviteId = Guid.NewGuid();
        var pending = new PendingRoomInvite(inviteId, room.Id, host, invitee);
        _pendingInvites[inviteId] = pending;
        await SendToUserAsync(invitee, Message.Create(MessageType.RoomInvite,
            new RoomInvitePayload(inviteId, room.Id, host, invitee)), ct);
    }

    private async Task AcceptRoomInviteAsync(IClientHandler session, RoomInviteAcceptedPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var invitee) || request is null ||
            !_pendingInvites.TryRemove(request.InviteId, out var pending) ||
            !pending.Invitee.Equals(invitee, StringComparison.OrdinalIgnoreCase)) return;

        var result = _conversations.AddMember(pending.RoomId, pending.Host, invitee, out var conversation);
        if (result != ConversationOperation.Success && result != ConversationOperation.AlreadyMember)
        {
            await SendRoomErrorAsync(session, result, ct);
            return;
        }
        if (conversation is not null)
        {
            await BroadcastConversationUpdateAsync(conversation, ct);
            await SendToUserAsync(pending.Host, Message.Create(MessageType.RoomInviteAccepted,
                new RoomInviteAcceptedPayload(pending.InviteId, pending.RoomId, invitee)), ct);
        }
    }

    private async Task RejectRoomInviteAsync(IClientHandler session, RoomInviteRejectedPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var invitee) || request is null ||
            !_pendingInvites.TryRemove(request.InviteId, out var pending) ||
            !pending.Invitee.Equals(invitee, StringComparison.OrdinalIgnoreCase)) return;
        await SendToUserAsync(pending.Host, Message.Create(MessageType.RoomInviteRejected,
            new RoomInviteRejectedPayload(pending.InviteId, pending.RoomId, invitee)), ct);
    }

    private async Task JoinRoomAsync(IClientHandler session, JoinRoomRequestPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var result = _conversations.Join(request.RoomId, username, out var conversation);
        if (result is not (ConversationOperation.Success or ConversationOperation.AlreadyMember) || conversation is null)
        {
            await SendRoomErrorAsync(session, result, ct);
            return;
        }

        await BroadcastConversationUpdateAsync(conversation, ct);
    }

    private async Task LeaveConversationAsync(IClientHandler session, LeaveRoomRequestPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var result = _conversations.Leave(request.RoomId, username, out var conversation, out var removed);
        if (result != ConversationOperation.Success)
        {
            await SendRoomErrorAsync(session, result, ct);
            return;
        }

        _media.RemoveEndpoint(request.RoomId, username);
        if (removed) _media.ForgetConversation(request.RoomId);
        else if (conversation is not null) await BroadcastConversationUpdateAsync(conversation, ct);
    }

    private async Task StartGroupMediaAsync(IClientHandler session, StartRoomMediaPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        var result = _conversations.StartMedia(request.RoomId, username, out var conversation);
        if (result != ConversationOperation.Success || conversation is null)
        {
            await SendErrorAsync(session, ErrorCodes.NotRoomMember, "Only the host can start this conversation.", ct);
            return;
        }
        await StartMediaForConversationAsync(conversation.Id, ct);
    }

    private async Task StopGroupMediaAsync(IClientHandler session, StopRoomMediaPayload? request, CancellationToken ct)
    {
        if (!RequireAuthenticated(session, out var username) || request is null) return;
        _conversations.TryGet(request.RoomId, out var beforeStop);
        var result = _conversations.StopMedia(request.RoomId, username, out var conversation);
        if (result != ConversationOperation.Success || conversation is null) return;

        _media.ForgetConversation(conversation.Id);
        var message = Message.Create(MessageType.RoomMediaStopped,
            new RoomMediaPayload(conversation.Id, beforeStop?.MediaId ?? Guid.Empty));
        await SendToConversationAsync(conversation.Id, message, ct);
    }

    private async Task StartMediaForConversationAsync(string conversationId, CancellationToken ct)
    {
        if (!_conversations.TryGet(conversationId, out var activeConversation) || activeConversation.MediaId is not Guid mediaId)
            return;
        var message = Message.Create(MessageType.RoomMediaStarted,
            new RoomMediaPayload(conversationId, mediaId));
        await SendToConversationAsync(conversationId, message, ct);
    }

    private async Task BroadcastConversationUpdateAsync(Conversation conversation, CancellationToken ct)
    {
        await SendToConversationAsync(conversation.Id,
            Message.Create(MessageType.RoomUpdate,
                new RoomUpdatePayload(conversation.Id, conversation.Host, conversation.Members.ToList())), ct);
    }

    private async Task SendToConversationAsync(string conversationId, Message message, CancellationToken ct)
    {
        foreach (var member in _conversations.GetMembersSnapshot(conversationId))
            await SendToUserAsync(member, message, ct);
    }

    /// <summary>
    /// Ì‰›–  ‰ŸÌ› Ã·”… TCP »⁄œ «‰ﬁÿ«⁄ «·⁄„Ì·° „À· ≈“«·… «·Õ÷Ê—
    /// Ê≈Œ—«Ã «·„” Œœ„ „‰ «·„Õ«œÀ«  Ê≈»·«€ «·√⁄÷«¡ «·„ »ﬁÌ‰.
    /// </summary>
    public async Task HandleDisconnectAsync(IClientHandler session, CancellationToken ct)
    {
        if (session.Username is null) return;
        _presence.Remove(session.Username, session);

        foreach (var conversation in _conversations.RemoveUserFromAll(session.Username))
        {
            _media.RemoveEndpoint(conversation.Id, session.Username);
            await BroadcastConversationUpdateAsync(conversation, ct);
        }

        await BroadcastPresenceAsync(ct);
    }

    private async Task BroadcastPresenceAsync(CancellationToken ct)
    {
        var message = Message.Create(
            MessageType.OnlineUsersUpdate,
            new OnlineUsersUpdatePayload(_presence.GetUsernames().ToList()));
        foreach (var session in _presence.GetSessions())
        {
            try { await session.SendAsync(message, ct); }
            catch { /* Ì „  ‰ŸÌ› «·Ã·”… „‰ Œ·«· ClientSession. */ }
        }
    }

    /// <summary>
    /// ÌÕ«Ê· ≈—”«· —”«·… ≈·Ï „” Œœ„ „Õœœ° ÊÌ”Ã· «·Œÿ√ ⁄‰œ  ⁄–— «·≈—”«·.
    /// </summary>
    private async Task SendToUserAsync(string username, Message message, CancellationToken ct)
    {
        if (!_presence.TryGet(username, out var session)) return;
        try
        {
            await session.SendAsync(message, ct);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Could not send {message.Type} to {username}: {ex.Message}");
        }
    }

    private async Task SendRoomErrorAsync(IClientHandler session, ConversationOperation result, CancellationToken ct)
    {
        var (code, text) = result switch
        {
            ConversationOperation.AlreadyExists => (ErrorCodes.RoomAlreadyExists, "Room already exists."),
            ConversationOperation.NotFound => (ErrorCodes.RoomNotFound, "Room not found."),
            ConversationOperation.NotMember => (ErrorCodes.NotRoomMember, "You are not a room member."),
            ConversationOperation.Full => (ErrorCodes.RoomFull, "Room is full."),
            _ => (ErrorCodes.UnexpectedError, "Unexpected room error.")
        };
        await session.SendAsync(Message.Create(MessageType.RoomError,
            new RoomErrorPayload(code, text)), ct);
    }

    private static async Task SendErrorAsync(IClientHandler session, string code, string text, CancellationToken ct)
    {
        await session.SendAsync(Message.Create(MessageType.Error, new ErrorPayload(code, text)), ct);
    }

    private static bool RequireAuthenticated(IClientHandler session, out string username)
    {
        username = session.Username ?? string.Empty;
        return !string.IsNullOrWhiteSpace(username);
    }
}
