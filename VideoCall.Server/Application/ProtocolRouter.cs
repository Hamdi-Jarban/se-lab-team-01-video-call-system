using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Messages;

namespace VideoCall.Server.Application;

public sealed class ProtocolRouter
{
    private readonly IUserPresenceRepository _presence;
    private readonly IConversationRepository _conversations;

    // حقن مرجع للتحقق من بيانات الاعتماد الخاصة بمهمة تسجيل الدخول (Issue #1)
    private readonly ICredentialValidator _credentials;

    // Issue #3 يعتمد على SendAsync وSendToUserAsync
    // الموجودتين في كود الاتصال الأساسي للخادم.
    private readonly Func<string, Message, CancellationToken, Task> _sendToUserAsync;

    public ProtocolRouter(
        IUserPresenceRepository presence,
        IConversationRepository conversations,
        ICredentialValidator credentials,
        Func<string, Message, CancellationToken, Task> sendToUserAsync)
    {
        _presence = presence;
        _conversations = conversations;
        _credentials = credentials;
        _sendToUserAsync = sendToUserAsync;
    }

    /// <summary>
    /// موجه الرسائل الأساسي للعميل (يشمل تسيير المهمة الأولى والمهمة الثالثة).
    /// </summary>
    public async Task DispatchAsync(IClientHandler session, Message message, CancellationToken ct)
    {
        if (message is null)
            return;

        switch (message.Type)
        {
            // === مهمة تسجيل الدخول (Issue #1 - الخاصة بك) ===
            case MessageType.LoginRequest:
                await DispatchLoginAsync(session, message.ReadPayload<LoginRequestPayload>(), ct);
                return;

            // === طلب المكالمة (Issue #3 - الخاصة بزملائك) ===
            case MessageType.CallRequest:
                await DispatchCallRequestAsync(session, message.ReadPayload<CallRequestPayload>(), ct);
                return;

            default:
                break;
        }
    }

    #region === Issue #1: معالجة تسجيل الدخول (المهمة الخاصة بك) ===

    /// <summary>
    /// معالجة طلب تسجيل الدخول، التحقق من البيانات، وتحديث حالة التواجد (Presence).
    /// </summary>
    private async Task DispatchLoginAsync(
        IClientHandler session,
        LoginRequestPayload? request,
        CancellationToken ct)
    {
        if (request is null || session.IsAuthenticated)
        {
            await SendLoginErrorAsync(session, ErrorCodes.InvalidCredentials, ct);
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

        await session.SendAsync(Message.Create(
            MessageType.LoginResponse,
            new LoginResponsePayload(true, null, username, session.SessionToken)), ct);
    }

    private static Task SendLoginErrorAsync(IClientHandler session, string errorCode, CancellationToken ct)
    {
        return session.SendAsync(
            Message.Create(
                MessageType.LoginResponse,
                new LoginResponsePayload(false, errorCode, null, null)),
            ct);
    }

    #endregion

    #region === Issue #3: معالجة طلبات المكالمة (كود زملائك الأصلي كاملاً) ===

    /// <summary>
    /// Issue #3: توجيه CallRequest إلى منطق إرسال طلب المكالمة.
    /// </summary>
    public async Task DispatchCallRequestAsync( IClientHandler session,CallRequestPayload? request,
        CancellationToken ct)
    {
        if (!session.IsAuthenticated ||
            string.IsNullOrWhiteSpace(session.Username) ||
            request is null)
        {
            return;
        }

        var caller = session.Username;
        var callee = request.Callee?.Trim();

        if (string.IsNullOrWhiteSpace(callee) ||
            caller.Equals(
                callee,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Issue #3 يعتمد على خدمة Presence لمعرفة المستخدمين المتصلين.
        if (!_presence.IsOnline(callee))
        {
            await SendErrorAsync(
                session,
                ErrorCodes.TargetOffline,
                "The target user is offline.",
                ct);

            return;
        }

        var callId = Guid.NewGuid();

        var result = _conversations.CreatePrivate(
            callId.ToString("N"),
            caller,
            callee,
            out _);

        if (result == ConversationOperation.Busy)
        {
            await SendErrorAsync(
                session,
                ErrorCodes.TargetBusy,
                "The target user is busy.",
                ct);

            return;
        }

        if (result != ConversationOperation.Success)
        {
            await SendErrorAsync(
                session,
                ErrorCodes.UnexpectedError,
                "The call request could not be created.",
                ct);

            return;
        }

        var message = Message.Create(
            MessageType.CallRequest,
            new CallRequestPayload(
                callId,
                caller,
                callee));

        // Issue #3: إرسال الطلب إلى المستخدم المستهدف.
        await _sendToUserAsync(callee, message, ct);

        // Issue #3: إعادة الطلب إلى المرسل مع CallId الحقيقي.
        await _sendToUserAsync(caller, message, ct);
    }

    // يعتمد Issue #3 على SendAsync الموجودة في كود جلسة الخادم.
    private static Task SendErrorAsync(IClientHandler session,string errorCode,string message,CancellationToken ct)
    {
        return session.SendAsync(
            Message.Create(
                MessageType.CallError,
                new ErrorPayload(errorCode, message)),
            ct);
    }

    #endregion
}