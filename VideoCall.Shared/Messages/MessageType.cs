namespace VideoCall.Shared.Messages;

// تعريف جميع أنواع الرسائل التي يمكن إرسالها عبر قناة الاتصال TCP
public enum MessageType
{
    // طلب تسجيل الدخول
    LoginRequest,

    // استجابة تسجيل الدخول
    LoginResponse,

    // تحديث قائمة المستخدمين المتصلين
    OnlineUsersUpdate,

    // طلب بدء مكالمة
    CallRequest,

    // قبول المكالمة
    CallAccepted,

    // رفض المكالمة
    CallRejected,

    // إنهاء المكالمة
    CallEnded,

    // انتهاء مهلة المكالمة
    CallTimedOut,

    // خطأ متعلق بالمكالمة
    CallError,

    // طلب إنشاء غرفة
    CreateRoomRequest,

    // طلب إضافة مستخدم إلى الغرفة
    AddUserToRoomRequest,

    // طلب الانضمام إلى الغرفة
    JoinRoomRequest,

    // طلب مغادرة الغرفة
    LeaveRoomRequest,

    // تحديث بيانات الغرفة
    RoomUpdate,

    // خطأ متعلق بالغرفة
    RoomError,

    // طلب بدء الوسائط داخل الغرفة
    StartRoomMedia,

    // طلب إيقاف الوسائط داخل الغرفة
    StopRoomMedia,

    // إشعار ببدء الوسائط داخل الغرفة
    RoomMediaStarted,

    // إشعار بإيقاف الوسائط داخل الغرفة
    RoomMediaStopped,

    // إرسال دعوة إلى مستخدم للانضمام إلى الغرفة
    RoomInvite,

    // قبول دعوة الغرفة
    RoomInviteAccepted,

    // رفض دعوة الغرفة
    RoomInviteRejected,

    // رسالة خطأ عامة
    Error,

    // قطع الاتصال
    Disconnect,

    // إيقاف وسائط المحادثة
    StopConversationMedia,

    // بدء وسائط المحادثة
    StartConversationMedia
}