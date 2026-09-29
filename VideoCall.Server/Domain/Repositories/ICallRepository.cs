namespace VideoCall.Server.Domain.Repositories;

// الوصول إلى جدول Calls (سجل فقط، لا تُخزَّن حزم الصوت/الفيديو)
public interface ICallRepository
{
    Task CreateAsync(Guid callId, int? conversationId, string? roomId, int startedByUserId, CallType type, CallStatus status, CancellationToken ct);

    Task UpdateStatusAsync(Guid callId, CallStatus status, CancellationToken ct);

    // يغلق سجلات المكالمات غير المنتهية عند بدء الخادم
    Task MarkStaleAsInterruptedAsync(CancellationToken ct);
}
