namespace VideoCall.Server.Domain;

/// <summary>
/// يوفّر العمليات اللازمة لإدارة نقاط اتصال UDP المرتبطة بالمحادثات.
/// </summary>
public interface IMediaRelayCoordinator
{
    /// <summary>
    /// يحذف نقطة اتصال مستخدم من محادثة محددة.
    /// </summary>
    /// <param name="conversationId">معرف المحادثة.</param>
    /// <param name="username">اسم المستخدم.</param>
    void RemoveEndpoint(string conversationId, string username);

    /// <summary>
    /// يحذف جميع نقاط الاتصال المرتبطة بالمحادثة.
    /// </summary>
    /// <param name="conversationId">معرف المحادثة.</param>
    void ForgetConversation(string conversationId);
}
