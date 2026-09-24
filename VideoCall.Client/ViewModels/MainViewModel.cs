using System.Windows;
using VideoCall.Client.Contracts;
using VideoCall.Shared.Messages;

namespace VideoCall.Client.ViewModels;

public sealed class MainViewModel : ViewModelBase, IDisposable
{
    private readonly INetworkClient _network;

    private bool _isCallRequestPending;
    private string? _pendingCallee;
    private Guid? _pendingCallId;
    private string _status = string.Empty;

    public bool IsCallRequestPending
    {
        get => _isCallRequestPending;
        private set => SetField(ref _isCallRequestPending, value);
    }

    public string? PendingCallee
    {
        get => _pendingCallee;
        private set => SetField(ref _pendingCallee, value);
    }

    public string Status
    {
        get => _status;
        private set => SetField(ref _status, value);
    }

    public MainViewModel(INetworkClient network)
    {
        _network = network;

        // Issue #3 يعتمد على حالة اتصال TCP التي يوفرها NetworkClient.
        _network.CallTimedOut += OnCallTimedOut;

        // Issue #3 يعتمد على أخطاء الخادم لإلغاء حالة الانتظار.
        _network.CallError += OnCallError;

        // Issue #3 يعتمد على CallRequest المعاد من الخادم للحصول على CallId.
        _network.IncomingCall += OnIncomingCall;
    }

    /// <summary>
    /// Issue #3: إرسال طلب مكالمة ومنع الطلبات المتعددة.
    /// </summary>
    public async Task RequestPrivateCallAsync(string username)
    {
        if (!_network.IsConnected)
        {
            Status = "الخادم غير متصل.";
            return;
        }

        if (IsCallRequestPending)
        {
            Status = "يوجد طلب مكالمة قيد الانتظار.";
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        PendingCallee = username.Trim();
        IsCallRequestPending = true;
        Status = $"بانتظار الرد من {PendingCallee}...";

        try
        {
            await _network.RequestCallAsync(PendingCallee);
        }
        catch
        {
            ClearPendingCall("تعذر إرسال طلب المكالمة.");
        }
    }

    /// <summary>
    /// Issue #3: حفظ CallId الذي أنشأه الخادم.
    /// </summary>
    private void OnIncomingCall(CallRequestPayload payload)
    {
        if (!string.Equals(
                payload.Caller,
                _network.Username,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.Equals(
                payload.Callee,
                PendingCallee,
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        _pendingCallId = payload.CallId;
    }

    /// <summary>
    /// Issue #3: إلغاء الانتظار عند انتهاء مهلة الطلب.
    /// </summary>
    private void OnCallTimedOut(CallTimedOutPayload payload)
    {
        if (_pendingCallId.HasValue &&
            payload.CallId != _pendingCallId.Value)
        {
            return;
        }

        ClearPendingCall("انتهت مهلة انتظار الرد.");
    }

    /// <summary>
    /// Issue #3: إلغاء الانتظار عند وصول خطأ من الخادم.
    /// </summary>
    private void OnCallError(ErrorPayload error)
    {
        ClearPendingCall(error.Message);
    }

    /// <summary>
    /// Issue #3: إعادة الواجهة إلى حالة السماح بطلب جديد.
    /// </summary>
    private void ClearPendingCall(string message)
    {
        IsCallRequestPending = false;
        PendingCallee = null;
        _pendingCallId = null;
        Status = message;
    }

    public void Dispose()
    {
        // Issue #3: إزالة الاشتراكات حتى لا تبقى أحداث مرتبطة بعد إغلاق الواجهة.
        _network.CallTimedOut -= OnCallTimedOut;
        _network.CallError -= OnCallError;
        _network.IncomingCall -= OnIncomingCall;
    }
}
