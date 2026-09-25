using VideoCall.Shared.Messages;

namespace VideoCall.Client.Contracts;

/// واجهة (Interface) لإدارة الاتصال الشبكي بقناة التحكم (TCP) مع السيرفر.
/// مسؤولة عن عمليات: تسجيل الدخول، حالة الاتصال، طلبات المكالمات، وإدارة الغرف.
/// ملاحظة معمارية: تعتمد جميع واجهات المستخدم (ViewModels) على هذه الواجهة (INetworkClient)
/// بدلاً من الكلاس الفعلي (NetworkClient) تطبيقاً لمبدأ (Dependency Inversion Principle)، 
/// كما يتم استخدام نمط (Observer Pattern) للاستماع لرسائل السيرفر عبر الأحداث (Events).
public interface INetworkClient : IDisposable
{
    // خصائص حالة الاتصال والمستخدم
    string? Username { get; }
    Guid? SessionToken { get; }
    string? ServerHost { get; }
    bool IsConnected { get; }

    // أحداث (Events) نمط Observer للاستجابة لردود السيرفر (تسجيل دخول، مكالمات، غرف)
    event Action? Disconnected;
    event Action<LoginResponsePayload>? LoginResponseReceived;
    event Action<OnlineUsersUpdatePayload>? OnlineUsersUpdated;
    event Action<CallRequestPayload>? IncomingCall;
    event Action<CallAcceptedPayload>? CallAccepted;
    event Action<CallRejectedPayload>? CallRejected;
    event Action<CallEndedPayload>? CallEnded;
    event Action<CallTimedOutPayload>? CallTimedOut;
    event Action<ErrorPayload>? CallError;
    event Action<ErrorPayload>? ErrorReceived;
    event Action<RoomUpdatePayload>? RoomUpdated;
    event Action<RoomErrorPayload>? RoomError;
    event Action<RoomMediaPayload>? RoomMediaStarted;
    event Action<RoomMediaPayload>? RoomMediaStopped;
    event Action<RoomInvitePayload>? RoomInviteReceived;
    event Action<RoomInviteAcceptedPayload>? RoomInviteAccepted;
    event Action<RoomInviteRejectedPayload>? RoomInviteRejected;

    // دوال (Methods) لإرسال الطلبات إلى السيرفر
    Task<bool> ConnectAsync(string host);
    Task LoginAsync(string username, string password);
    Task RequestCallAsync(string callee);
    Task AcceptCallAsync(Guid callId, string caller);
    Task RejectCallAsync(Guid callId, string caller);
    Task EndCallAsync(Guid callId);
    Task CreateRoomAsync(string roomId);
    Task AddUserToRoomAsync(string roomId, string username);
    Task AcceptRoomInviteAsync(RoomInvitePayload invite);
    Task RejectRoomInviteAsync(RoomInvitePayload invite);
    Task JoinRoomAsync(string roomId);
    Task LeaveRoomAsync(string roomId);
    Task StartRoomMediaAsync(string roomId);
    Task StopRoomMediaAsync(string roomId, Guid mediaId);
}