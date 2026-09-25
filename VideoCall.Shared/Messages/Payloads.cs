namespace VideoCall.Shared.Messages;

// بيانات طلب تسجيل الدخول
public record LoginRequestPayload(string Username, string Password);

// بيانات استجابة تسجيل الدخول
public record LoginResponsePayload(bool Success, string? ErrorCode, string? Username, Guid? SessionToken);

// بيانات تحديث قائمة المستخدمين المتصلين
public record OnlineUsersUpdatePayload(List<string> Usernames);

// بيانات طلب بدء المكالمة
public record CallRequestPayload(Guid CallId, string Caller, string Callee);

// بيانات قبول المكالمة
public record CallAcceptedPayload(Guid CallId, string Caller, string Callee);

// بيانات رفض المكالمة
public record CallRejectedPayload(Guid CallId, string Caller, string Callee);

// بيانات إنهاء المكالمة
public record CallEndedPayload(Guid CallId, string EndedBy);

// بيانات انتهاء مهلة المكالمة
public record CallTimedOutPayload(Guid CallId);

// بيانات الخطأ الخاص بالمكالمة
public record CallErrorPayload(string ErrorCode, string Message);

// بيانات طلب إنشاء غرفة
public record CreateRoomRequestPayload(string RoomId);

// بيانات طلب إضافة مستخدم إلى الغرفة
public record AddUserToRoomRequestPayload(string RoomId, string Username);

// بيانات طلب الانضمام إلى الغرفة
public record JoinRoomRequestPayload(string RoomId);

// بيانات طلب مغادرة الغرفة
public record LeaveRoomRequestPayload(string RoomId);

// بيانات تحديث معلومات الغرفة وأعضائها
public record RoomUpdatePayload(string RoomId, string Host, List<string> Members);

// بيانات الخطأ الخاص بالغرفة
public record RoomErrorPayload(string ErrorCode, string Message);

// بيانات طلب بدء الوسائط داخل الغرفة
public record StartRoomMediaPayload(string RoomId, Guid MediaId);

// بيانات طلب إيقاف الوسائط داخل الغرفة
public record StopRoomMediaPayload(string RoomId, Guid MediaId);

// بيانات الوسائط الخاصة بالغرفة
public record RoomMediaPayload(string RoomId, Guid MediaId);

// بيانات دعوة مستخدم إلى الغرفة
public record RoomInvitePayload(Guid InviteId, string RoomId, string Host, string Invitee);

// بيانات قبول دعوة الغرفة
public record RoomInviteAcceptedPayload(Guid InviteId, string RoomId, string Invitee);

// بيانات رفض دعوة الغرفة
public record RoomInviteRejectedPayload(Guid InviteId, string RoomId, string Invitee);

// بيانات رسالة الخطأ العامة
public record ErrorPayload(string ErrorCode, string Message);

// تعريف رموز الأخطاء المستخدمة بشكل مشترك بين أجزاء النظام
public static class ErrorCodes
{
    // رمز يدل على أن بيانات تسجيل الدخول غير صحيحة
    public const string InvalidCredentials = "INVALID_CREDENTIALS";

    // رمز يدل على أن المستخدم مسجل الدخول مسبقًا
    public const string AlreadyLoggedIn = "ALREADY_LOGGED_IN";

    // رمز يدل على أن المستخدم المستهدف غير متصل
    public const string TargetOffline = "TARGET_OFFLINE";

    // رمز يدل على أن المستخدم المستهدف مشغول
    public const string TargetBusy = "TARGET_BUSY";

    // رمز يدل على أن المكالمة غير موجودة
    public const string CallNotFound = "CALL_NOT_FOUND";

    // رمز يدل على أن المستخدم ليس طرفًا في المكالمة
    public const string NotYourCall = "NOT_YOUR_CALL";

    // رمز يدل على أن حالة المكالمة غير صالحة
    public const string InvalidCallState = "INVALID_CALL_STATE";

    // رمز يدل على أن الغرفة موجودة مسبقًا
    public const string RoomAlreadyExists = "ROOM_ALREADY_EXISTS";

    // رمز يدل على أن الغرفة غير موجودة
    public const string RoomNotFound = "ROOM_NOT_FOUND";

    // رمز يدل على أن الغرفة ممتلئة
    public const string RoomFull = "ROOM_FULL";

    // رمز يدل على أن الوسائط بدأت مسبقًا
    public const string MediaAlreadyStarted = "MEDIA_ALREADY_STARTED";

    // رمز يدل على أن الوسائط لم تبدأ بعد
    public const string MediaNotStarted = "MEDIA_NOT_STARTED";

    // رمز يدل على أن المستخدم ليس عضوًا في الغرفة
    public const string NotRoomMember = "NOT_ROOM_MEMBER";

    // رمز يدل على أن المستخدم غير موجود
    public const string UserNotFound = "USER_NOT_FOUND";

    // رمز يدل على أن الخادم غير متاح
    public const string ServerUnavailable = "SERVER_UNAVAILABLE";

    // رمز يدل على حدوث خطأ غير متوقع
    public const string UnexpectedError = "UNEXPECTED_ERROR";
}