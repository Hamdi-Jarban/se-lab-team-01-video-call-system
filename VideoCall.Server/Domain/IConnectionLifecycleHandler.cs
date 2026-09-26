namespace VideoCall.Server.Domain;

/// <summary>
/// مسؤول عن تنفيذ عمليات تنظيف جلسة العميل بعد إغلاق اتصال TCP،
/// مثل إزالة المستخدم من قائمة المتصلين وتحديث المحادثات المرتبطة به.
/// </summary>
public interface IConnectionLifecycleHandler
{
    /// <summary>
    /// يعالج إغلاق جلسة العميل وينفذ عمليات التنظيف المطلوبة.
    /// </summary>
    /// <param name="session">جلسة العميل التي أُغلقت.</param>
    /// <param name="ct">رمز إلغاء العملية.</param>
    Task HandleDisconnectAsync(IClientHandler session,CancellationToken ct);
}
