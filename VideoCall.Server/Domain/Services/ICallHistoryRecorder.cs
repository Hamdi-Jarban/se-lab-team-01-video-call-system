using VideoCall.Server.Domain.Repositories;

namespace VideoCall.Server.Domain.Services;

// تسجيل سجل المكالمات. أي فشل في قاعدة البيانات لا يجب أن يعطّل المكالمة نفسها،
// لذلك التنفيذ يبتلع الأخطاء ويسجلها فقط.
public interface ICallHistoryRecorder
{
    Task PrivateCallStartedAsync(Guid callId, int callerUserId);

    Task RoomCallStartedAsync(Guid callId, string roomId, int startedByUserId);

    Task StatusChangedAsync(Guid callId, CallStatus status);
}
