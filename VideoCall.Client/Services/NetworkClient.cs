using System.Net.Sockets;
using System.Windows;
using VideoCall.Client.Contracts;
using VideoCall.Shared.Messages;
using VideoCall.Shared.Networking;

namespace VideoCall.Client.Services;

/// تنفيذ عقد عميل الشبكة: إدارة اتصال TCP، قراءة الرسائل في الخلفية، وتحويل الأحداث لخيط واجهة المستخدم.
public class NetworkClient : INetworkClient
{
    private TcpClient? _tcpClient;
    private TcpMessageReaderWriter? _framing;
    private CancellationTokenSource? _cts;

    public string? Username { get; private set; }
    public Guid? SessionToken { get; private set; }
    public string? ServerHost { get; private set; }
    public bool IsConnected => _tcpClient?.Connected ?? false;

    // الأحداث البرمجية لتنبيه الواجهة عند استقبال رسائل أو تغيرات حالة الاتصال
    public event Action? Disconnected;
    public event Action<LoginResponsePayload>? LoginResponseReceived;
    public event Action<OnlineUsersUpdatePayload>? OnlineUsersUpdated;
    public event Action<CallRequestPayload>? IncomingCall;
    public event Action<CallAcceptedPayload>? CallAccepted;
    public event Action<CallRejectedPayload>? CallRejected;
    public event Action<CallEndedPayload>? CallEnded;
    public event Action<CallTimedOutPayload>? CallTimedOut;
    public event Action<ErrorPayload>? CallError;
    public event Action<ErrorPayload>? ErrorReceived;
    public event Action<RoomUpdatePayload>? RoomUpdated;
    public event Action<RoomErrorPayload>? RoomError;
    public event Action<RoomMediaPayload>? RoomMediaStarted;
    public event Action<RoomMediaPayload>? RoomMediaStopped;
    public event Action<RoomInvitePayload>? RoomInviteReceived;
    public event Action<RoomInviteAcceptedPayload>? RoomInviteAccepted;
    public event Action<RoomInviteRejectedPayload>? RoomInviteRejected;

    public event Action<RegisterResponsePayload>? RegisterResponseReceived;
    public event Action<ConversationOpenedPayload>? ConversationOpened;
    public event Action<ConversationMembersPayload>? ConversationMembersUpdated;
    public event Action<ConversationRemovedPayload>? ConversationRemoved;
    public event Action<MessageReceivedPayload>? ChatMessageReceived;
    public event Action<GetConversationsResponsePayload>? ConversationsLoaded;
    public event Action<GetMessagesResponsePayload>? MessagesLoaded;
    public event Action<ChatErrorPayload>? ChatErrorReceived;
    public event Action<MessageEditedPayload>? MessageEdited;
    public event Action<MessageDeletedPayload>? MessageDeleted;

    /// الاتصال بخادم التحكم عبر بروتوكول TCP وبدء حلقة الاستماع الخلفية.
    public async Task<bool> ConnectAsync(string host)
    {
        try
        {
            _disconnectedRaised = false;
            var generation = Interlocked.Increment(ref _generation);
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, NetworkConfig.TcpControlPort);
            ServerHost = host;
            _framing = new TcpMessageReaderWriter(_tcpClient.GetStream());
            _cts = new CancellationTokenSource();
            _ = ReadLoopAsync(_cts.Token, generation);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public Task LoginAsync(string username, string password) =>
        SendAsync(Message.Create(MessageType.LoginRequest, new LoginRequestPayload(username, password)));

    public Task RequestCallAsync(string callee) =>
        SendAsync(Message.Create(MessageType.CallRequest, new CallRequestPayload(Guid.Empty, Username ?? "", callee)));

    public Task AcceptCallAsync(Guid callId, string caller) =>
        SendAsync(Message.Create(MessageType.CallAccepted, new CallAcceptedPayload(callId, caller, Username ?? "")));

    public Task RejectCallAsync(Guid callId, string caller) =>
        SendAsync(Message.Create(MessageType.CallRejected, new CallRejectedPayload(callId, caller, Username ?? "")));

    public Task EndCallAsync(Guid callId) =>
        SendAsync(Message.Create(MessageType.CallEnded, new CallEndedPayload(callId, Username ?? "")));

    public Task CreateRoomAsync(string roomId) =>
        SendAsync(Message.Create(MessageType.CreateRoomRequest, new CreateRoomRequestPayload(roomId)));

    public Task AddUserToRoomAsync(string roomId, string username) =>
        SendAsync(Message.Create(MessageType.AddUserToRoomRequest, new AddUserToRoomRequestPayload(roomId, username)));

    public Task AcceptRoomInviteAsync(RoomInvitePayload invite) =>
        SendAsync(Message.Create(MessageType.RoomInviteAccepted,
            new RoomInviteAcceptedPayload(invite.InviteId, invite.RoomId, Username ?? string.Empty)));

    public Task RejectRoomInviteAsync(RoomInvitePayload invite) =>
        SendAsync(Message.Create(MessageType.RoomInviteRejected,
            new RoomInviteRejectedPayload(invite.InviteId, invite.RoomId, Username ?? string.Empty)));

    public Task JoinRoomAsync(string roomId) =>
        SendAsync(Message.Create(MessageType.JoinRoomRequest, new JoinRoomRequestPayload(roomId)));

    public Task LeaveRoomAsync(string roomId) =>
        SendAsync(Message.Create(MessageType.LeaveRoomRequest, new LeaveRoomRequestPayload(roomId)));

    public Task StartRoomMediaAsync(string roomId) =>
        SendAsync(Message.Create(MessageType.StartRoomMedia,
            new StartRoomMediaPayload(roomId, Guid.Empty)));

    public Task StopRoomMediaAsync(string roomId, Guid mediaId) =>
        SendAsync(Message.Create(MessageType.StopRoomMedia,
            new StopRoomMediaPayload(roomId, mediaId)));

    /// إرسال رسالة مسلسلة عبر اتصال TCP.
    public Task RegisterAsync(string username, string password, string? displayName) =>
        SendAsync(Message.Create(MessageType.RegisterRequest, new RegisterRequestPayload(username, password, displayName)));

    public async Task LogoutAsync()
    {
        // نخبر الخادم أولًا ليُغلق الجلسة في SQL، ثم نُنهي الاتصال محليًا
        await SendAsync(Message.Create(MessageType.Disconnect, new ErrorPayload("LOGOUT", "Client logout")));
        ResetConnection();
    }

    public Task OpenPrivateChatAsync(string username) =>
        SendAsync(Message.Create(MessageType.OpenPrivateChatRequest, new OpenPrivateChatRequestPayload(username)));

    public Task CreateGroupAsync(string name, IReadOnlyList<string> memberUsernames) =>
        SendAsync(Message.Create(MessageType.CreateGroupRequest, new CreateGroupRequestPayload(name, memberUsernames.ToList())));

    public Task AddGroupMembersAsync(int conversationId, IReadOnlyList<string> usernames) =>
        SendAsync(Message.Create(MessageType.AddGroupMembersRequest, new AddGroupMembersRequestPayload(conversationId, usernames.ToList())));

    public Task RemoveGroupMemberAsync(int conversationId, string username) =>
        SendAsync(Message.Create(MessageType.RemoveGroupMemberRequest, new RemoveGroupMemberRequestPayload(conversationId, username)));

    public Task GetGroupMembersAsync(int conversationId) =>
        SendAsync(Message.Create(MessageType.GetGroupMembersRequest, new GetGroupMembersRequestPayload(conversationId)));

    public Task SendChatMessageAsync(int conversationId, string content) =>
        SendAsync(Message.Create(MessageType.SendMessageRequest, new SendMessageRequestPayload(conversationId, content)));

    public Task EditChatMessageAsync(int conversationId, long messageId, string content) =>
        SendAsync(Message.Create(MessageType.EditMessageRequest, new EditMessageRequestPayload(conversationId, messageId, content)));

    public Task DeleteChatMessageAsync(int conversationId, long messageId) =>
        SendAsync(Message.Create(MessageType.DeleteMessageRequest, new DeleteMessageRequestPayload(conversationId, messageId)));

    public Task GetConversationsAsync() =>
        SendAsync(Message.Create(MessageType.GetConversationsRequest, new GetConversationsRequestPayload()));

    public Task GetMessagesAsync(int conversationId, long? beforeMessageId = null, int limit = 50) =>
        SendAsync(Message.Create(MessageType.GetMessagesRequest, new GetMessagesRequestPayload(conversationId, beforeMessageId, limit)));

    private void ResetConnection()
    {
        Interlocked.Increment(ref _generation);
        _disconnectedRaised = true;

        try { _cts?.Cancel(); } catch { }
        try { _tcpClient?.Close(); } catch { }

        _tcpClient = null;
        _framing = null;
        _cts = null;
        Username = null;
        SessionToken = null;
    }

    private async Task SendAsync(Message message)
    {
        if (_framing is null || _cts is null)
        {
            return;
        }

        try
        {
            await _framing.WriteMessageAsync(message, _cts.Token).ConfigureAwait(false);
        }
        catch (Exception)
        {
            RaiseDisconnected();
        }
    }

    /// حلقة القراءة الخلفية لاستقبال الرسائل القادمة من السيرفر باستمرار.
    private async Task ReadLoopAsync(CancellationToken ct, int generation)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                Message? message;
                try
                {
                    message = await _framing!.ReadMessageAsync(ct).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    break;
                }

                if (message is null)
                {
                    break;
                }

                HandleMessage(message);
            }
        }
        finally
        {
            // اتصال قديم أُغلق عمدًا (تسجيل خروج) لا يجب أن يُطلق Disconnected لاتصال جديد
            if (generation == Volatile.Read(ref _generation)) RaiseDisconnected();
        }
    }

    /// توجيه ومعالجة الرسائل الواردة بناءً على نوعها.
    private void HandleMessage(Message message)
    {
        switch (message.Type)
        {
            case MessageType.LoginResponse:
                var login = message.ReadPayload<LoginResponsePayload>()!;
                if (login.Success)
                {
                    Username = login.Username;
                    SessionToken = login.SessionToken;
                }
                Raise(() => LoginResponseReceived?.Invoke(login));
                break;

            case MessageType.OnlineUsersUpdate:
                Raise(() => OnlineUsersUpdated?.Invoke(message.ReadPayload<OnlineUsersUpdatePayload>()!));
                break;

            case MessageType.CallRequest:
                Raise(() => IncomingCall?.Invoke(message.ReadPayload<CallRequestPayload>()!));
                break;

            case MessageType.CallAccepted:
                Raise(() => CallAccepted?.Invoke(message.ReadPayload<CallAcceptedPayload>()!));
                break;

            case MessageType.CallRejected:
                Raise(() => CallRejected?.Invoke(message.ReadPayload<CallRejectedPayload>()!));
                break;

            case MessageType.CallEnded:
                Raise(() => CallEnded?.Invoke(message.ReadPayload<CallEndedPayload>()!));
                break;

            case MessageType.CallTimedOut:
                Raise(() => CallTimedOut?.Invoke(message.ReadPayload<CallTimedOutPayload>()!));
                break;

            case MessageType.CallError:
                Raise(() => CallError?.Invoke(message.ReadPayload<ErrorPayload>()!));
                break;

            case MessageType.Error:
                Raise(() => ErrorReceived?.Invoke(message.ReadPayload<ErrorPayload>()!));
                break;

            case MessageType.RoomUpdate:
                Raise(() => RoomUpdated?.Invoke(message.ReadPayload<RoomUpdatePayload>()!));
                break;

            case MessageType.RoomError:
                Raise(() => RoomError?.Invoke(message.ReadPayload<RoomErrorPayload>()!));
                break;

            case MessageType.RoomMediaStarted:
                Raise(() => RoomMediaStarted?.Invoke(message.ReadPayload<RoomMediaPayload>()!));
                break;

            case MessageType.RoomMediaStopped:
                Raise(() => RoomMediaStopped?.Invoke(message.ReadPayload<RoomMediaPayload>()!));
                break;

            case MessageType.RoomInvite:
                Raise(() => RoomInviteReceived?.Invoke(message.ReadPayload<RoomInvitePayload>()!));
                break;
            case MessageType.RoomInviteAccepted:
                Raise(() => RoomInviteAccepted?.Invoke(message.ReadPayload<RoomInviteAcceptedPayload>()!));
                break;
            case MessageType.RoomInviteRejected:
                Raise(() => RoomInviteRejected?.Invoke(message.ReadPayload<RoomInviteRejectedPayload>()!));
                break;

            case MessageType.RegisterResponse:
                DispatchPayload<RegisterResponsePayload>(message, p => RegisterResponseReceived?.Invoke(p));
                break;

            case MessageType.ConversationOpened:
                DispatchPayload<ConversationOpenedPayload>(message, p => ConversationOpened?.Invoke(p));
                break;

            case MessageType.ConversationMembersUpdate:
                DispatchPayload<ConversationMembersPayload>(message, p => ConversationMembersUpdated?.Invoke(p));
                break;

            case MessageType.ConversationRemoved:
                DispatchPayload<ConversationRemovedPayload>(message, p => ConversationRemoved?.Invoke(p));
                break;

            case MessageType.MessageReceived:
                DispatchPayload<MessageReceivedPayload>(message, p => ChatMessageReceived?.Invoke(p));
                break;

            case MessageType.GetConversationsResponse:
                DispatchPayload<GetConversationsResponsePayload>(message, p => ConversationsLoaded?.Invoke(p));
                break;

            case MessageType.GetMessagesResponse:
                DispatchPayload<GetMessagesResponsePayload>(message, p => MessagesLoaded?.Invoke(p));
                break;

            case MessageType.MessageEdited:
                DispatchPayload<MessageEditedPayload>(message, p => MessageEdited?.Invoke(p));
                break;

            case MessageType.MessageDeleted:
                DispatchPayload<MessageDeletedPayload>(message, p => MessageDeleted?.Invoke(p));
                break;

            case MessageType.ChatError:
                DispatchPayload<ChatErrorPayload>(message, p => ChatErrorReceived?.Invoke(p));
                break;
        }
    }

    /// ضمان تنفيذ الأحداث على خيط واجهة المستخدم الخاص بـ WPF (Dispatcher).
    // يقرأ الـ Payload بأمان (payload تالف لا يُسقط حلقة القراءة) ثم يستدعي الحدث على خيط الواجهة
    private static void DispatchPayload<T>(Message message, Action<T> handler) where T : class
    {
        var payload = message.ReadPayload<T>();
        if (payload is null) return;
        Raise(() => handler(payload));
    }

    private static void Raise(Action action)
    {
        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(action);
        }
        else
        {
            action();
        }
    }

    private bool _disconnectedRaised;
    private int _generation;

    private void RaiseDisconnected()
    {
        if (_disconnectedRaised) return;
        _disconnectedRaised = true;
        Raise(() => Disconnected?.Invoke());
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _tcpClient?.Close();
    }
}