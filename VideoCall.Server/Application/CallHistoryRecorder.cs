using VideoCall.Server.Domain.Logging;
using VideoCall.Server.Domain.Repositories;
using VideoCall.Server.Domain.Services;

namespace VideoCall.Server.Application;

// يسجل المكالمات في SQL دون أن يؤثر فشل قاعدة البيانات على المكالمة الحية
public sealed class CallHistoryRecorder : ICallHistoryRecorder
{
    private readonly ICallRepository _calls;
    private readonly IAppLogger _logger;

    public CallHistoryRecorder(ICallRepository calls, IAppLogger logger)
    {
        _calls = calls ?? throw new ArgumentNullException(nameof(calls));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task PrivateCallStartedAsync(Guid callId, int callerUserId) =>
        SafeAsync(() => _calls.CreateAsync(callId, null, null, callerUserId, CallType.Private, CallStatus.Ringing, CancellationToken.None), "start private call");

    public Task RoomCallStartedAsync(Guid callId, string roomId, int startedByUserId) =>
        SafeAsync(() => _calls.CreateAsync(callId, null, roomId, startedByUserId, CallType.Room, CallStatus.Connected, CancellationToken.None), "start room call");

    public Task StatusChangedAsync(Guid callId, CallStatus status) =>
        SafeAsync(() => _calls.UpdateStatusAsync(callId, status, CancellationToken.None), $"set call status {status}");

    private async Task SafeAsync(Func<Task> action, string what)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            _logger.Warn($"Call history ({what}) failed: {ex.Message}");
        }
    }
}
