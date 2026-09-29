namespace VideoCall.Server.Domain.Repositories;

// الوصول إلى جدول UserSessions (سجل الجلسات فقط؛ حالة الاتصال الحية تبقى في الذاكرة)
public interface IUserSessionRepository
{
    Task CreateAsync(Guid sessionId, int userId, CancellationToken ct);

    Task EndAsync(Guid sessionId, CancellationToken ct);

    // يُستدعى عند بدء الخادم لإغلاق الجلسات التي بقيت مفتوحة بسبب إيقاف مفاجئ
    Task EndAllActiveAsync(CancellationToken ct);
}
