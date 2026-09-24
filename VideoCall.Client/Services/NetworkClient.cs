using VideoCall.Client.Contracts;
using VideoCall.Shared.Messages;

namespace VideoCall.Client.Services;

public partial class NetworkClient : INetworkClient
{
    // Issue #3 يعتمد على بيانات المستخدم التي يحددها كود تسجيل الدخول.
    public string? Username { get; private set; }

    // Issue #3 يعتمد على حالة الاتصال التي يديرها كود TCP الأساسي.
    public bool IsConnected { get; private set; }

    // Issue #3: يصل هذا الحدث عند وصول CallRequest من الخادم.
    public event Action<CallRequestPayload>? IncomingCall;

    // Issue #3: يصل هذا الحدث عند انتهاء مهلة طلب المكالمة.
    public event Action<CallTimedOutPayload>? CallTimedOut;

    // Issue #3: يصل هذا الحدث عند فشل طلب المكالمة.
    public event Action<ErrorPayload>? CallError;

    // Issue #3: إرسال طلب المكالمة باستخدام طريقة SendAsync التي سيضيفها عضو الشبكة.
    public Task RequestCallAsync(string callee)
    {
        if (!IsConnected)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(callee))
        {
            return Task.CompletedTask;
        }

        var payload = new CallRequestPayload(
            Guid.Empty,
            Username ?? string.Empty,
            callee.Trim());

        var message = Message.Create(
            MessageType.CallRequest,
            payload);

        // يعتمد هذا السطر على SendAsync الموجود في كود NetworkClient الأساسي.
        return SendAsync(message);
    }

    // هذا التعريف يوضح الاعتماد الخارجي فقط.
    // يجب أن تكون الدالة موجودة في NetworkClient الأساسي الذي سيضيفه عضو الشبكة.
    private Task SendAsync(Message message)
    {
        throw new NotImplementedException(
            "يتم توفير SendAsync من كود شبكة TCP الأساسي.");
    }

    // Issue #3: يستدعيها كود قراءة TCP عند وصول رسالة من الخادم.
    private void HandleIssue3Message(Message message)
    {
        switch (message.Type)
        {
            case MessageType.CallRequest:
                {
                    var request =
                        message.ReadPayload<CallRequestPayload>();

                    if (request is not null)
                    {
                        // يعتمد على كود Dispatcher الموجود في NetworkClient الأساسي.
                        IncomingCall?.Invoke(request);
                    }

                    break;
                }

            case MessageType.CallTimedOut:
                {
                    var timeout =
                        message.ReadPayload<CallTimedOutPayload>();

                    if (timeout is not null)
                    {
                        CallTimedOut?.Invoke(timeout);
                    }

                    break;
                }

            case MessageType.CallError:
                {
                    // يعتمد هذا الجزء على نوع الخطأ الذي يتفق عليه الفريق.
                    var error =
                        message.ReadPayload<ErrorPayload>();

                    if (error is not null)
                    {
                        CallError?.Invoke(error);
                    }

                    break;
                }
        }
    }

    public void Dispose()
    {
        // يعتمد التخلص من TCP على كود الشبكة الأساسي.
    }
}
