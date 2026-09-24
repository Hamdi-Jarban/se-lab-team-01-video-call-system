using VideoCall.Shared.Messages;

namespace VideoCall.Client.Contracts;

public interface INetworkClient : IDisposable
{
    string? Username { get; }

    bool IsConnected { get; }

    // Issue #3 يعتمد على وجود اتصال TCP جاهز قبل إرسال الطلب.
    Task RequestCallAsync(string callee);

    // Issue #3 يعتمد على هذا الحدث لإلغاء حالة الانتظار عند انتهاء المهلة.
    event Action<CallTimedOutPayload>? CallTimedOut;

    // Issue #3 يعتمد على هذا الحدث لعرض خطأ مثل المستخدم غير متصل أو مشغول.
    event Action<ErrorPayload>? CallError;

    // Issue #3 يعتمد على هذا الحدث للحصول على CallId الذي ينشئه الخادم.
    event Action<CallRequestPayload>? IncomingCall;
}
