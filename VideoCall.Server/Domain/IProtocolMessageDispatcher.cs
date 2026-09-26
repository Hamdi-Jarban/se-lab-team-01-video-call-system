using VideoCall.Shared.Messages;

namespace VideoCall.Server.Domain;

/// <summary>
/// يحوّل الرسائل الواردة من العميل إلى العمليات المناسبة داخل التطبيق،
/// مثل تسجيل الدخول وإدارة المكالمات والغرف.
/// </summary>
public interface IProtocolMessageDispatcher
{
    /// <summary>
    /// يعالج رسالة واردة من جلسة العميل.
    /// </summary>
    /// <param name="session">جلسة العميل المرسلة للرسالة.</param>
    /// <param name="message">الرسالة الواردة.</param>
    /// <param name="ct">رمز إلغاء العملية.</param>
    Task DispatchAsync(
        IClientHandler session,
        Message message,
        CancellationToken ct);
}
