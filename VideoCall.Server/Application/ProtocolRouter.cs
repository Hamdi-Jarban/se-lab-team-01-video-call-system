using VideoCall.Server.Domain;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Shared.Messages;

namespace VideoCall.Server.Application;

public sealed class ProtocolRouter
{
    private readonly IUserPresenceRepository _presence;
    private readonly IConversationRepository _conversations;

    // Issue #3 يعتمد على SendAsync وSendToUserAsync
    // الموجودتين في كود الاتصال الأساسي للخادم.
    private readonly Func<string, Message, CancellationToken, Task> _sendToUserAsync;

    public ProtocolRouter(
        IUserPresenceRepository presence,
        IConversationRepository conversations,
        Func<string, Message, CancellationToken, Task> sendToUserAsync)
    {
        _presence = presence;
        _conversations = conversations;
        _sendToUserAsync = sendToUserAsync;
    }

    /// <summary>
    /// Issue #3: توجيه CallRequest إلى منطق إرسال طلب المكالمة.
    /// </summary>
    public async Task DispatchCallRequestAsync(
        IClientHandler session,
        CallRequestPayload? request,
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
}
