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

// ===================== التسجيل والمراسلة الدائمة =====================

// طلب تسجيل حساب جديد (كلمة المرور تُرسل مرة واحدة وتُخزَّن كـ Hash فقط على الخادم)
public record RegisterRequestPayload(string Username, string Password, string? DisplayName);

// استجابة التسجيل
public record RegisterResponsePayload(bool Success, string? ErrorCode, string? Username);

// رسالة محادثة محفوظة (المرسل يُحدَّد من قبل الخادم فقط)
public record ChatMessageDto(long MessageId, int ConversationId, string SenderUsername, string SenderDisplayName, string Content, DateTime SentAtUtc, DateTime? EditedAtUtc = null);

// ملخص محادثة كما يراها مستخدم معين (اسم المحادثة الخاصة = اسم الطرف الآخر)
public record ConversationDto(int ConversationId, VideoCall.Shared.Models.ConversationType Type, string Name, string CreatedBy, List<string> Members, DateTime? LastMessageAtUtc, string? LastMessagePreview);

// فتح/إنشاء محادثة خاصة
public record OpenPrivateChatRequestPayload(string Username);

// إشعار بمحادثة (OpenedBy = من طلب الفتح/الإنشاء)
public record ConversationOpenedPayload(ConversationDto Conversation, string OpenedBy);

// إنشاء مجموعة
public record CreateGroupRequestPayload(string Name, List<string> MemberUsernames);

// إضافة أعضاء
public record AddGroupMembersRequestPayload(int ConversationId, List<string> Usernames);

// إزالة عضو (أو مغادرة إذا كان الاسم هو نفس المستخدم)
public record RemoveGroupMemberRequestPayload(int ConversationId, string Username);

// جلب الأعضاء
public record GetGroupMembersRequestPayload(int ConversationId);

// أعضاء المحادثة الحاليون
public record ConversationMembersPayload(int ConversationId, List<string> Members);

// إزالة المحادثة من قائمة المستخدم
public record ConversationRemovedPayload(int ConversationId);

// إرسال رسالة (لا يوجد SenderId: الخادم يحدده من الجلسة)
public record SendMessageRequestPayload(int ConversationId, string Content);

// رسالة تم حفظها وتوزيعها
public record MessageReceivedPayload(ChatMessageDto Message);

// طلب قائمة المحادثات
public record GetConversationsRequestPayload();

// استجابة قائمة المحادثات
public record GetConversationsResponsePayload(List<ConversationDto> Conversations);

// طلب سجل الرسائل (ترقيم بالصفحات: BeforeMessageId = null يعني أحدث الرسائل)
public record GetMessagesRequestPayload(int ConversationId, long? BeforeMessageId, int Limit);

// استجابة سجل الرسائل (مرتبة من الأقدم إلى الأحدث)
public record GetMessagesResponsePayload(int ConversationId, List<ChatMessageDto> Messages, bool HasMore);

// تعديل رسالة (الخادم يتحقق أن المستخدم هو صاحب الرسالة)
public record EditMessageRequestPayload(int ConversationId, long MessageId, string Content);

// حذف رسالة
public record DeleteMessageRequestPayload(int ConversationId, long MessageId);

// إشعار تعديل
public record MessageEditedPayload(int ConversationId, long MessageId, string Content, DateTime EditedAtUtc);

// إشعار حذف
public record MessageDeletedPayload(int ConversationId, long MessageId);

// خطأ المراسلة
public record ChatErrorPayload(string ErrorCode, string Message);

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

    // اسم المستخدم مستخدم مسبقًا
    public const string UsernameAlreadyExists = "USERNAME_ALREADY_EXISTS";

    // اسم مستخدم غير صالح
    public const string InvalidUsername = "INVALID_USERNAME";

    // كلمة مرور ضعيفة/غير صالحة
    public const string WeakPassword = "WEAK_PASSWORD";

    // يجب تسجيل الدخول أولًا
    public const string NotAuthenticated = "NOT_AUTHENTICATED";

    // المحادثة غير موجودة
    public const string ConversationNotFound = "CONVERSATION_NOT_FOUND";

    // المستخدم ليس عضوًا في المحادثة
    public const string NotConversationMember = "NOT_CONVERSATION_MEMBER";

    // العملية تتطلب مدير المجموعة
    public const string NotGroupAdmin = "NOT_GROUP_ADMIN";

    // محتوى الرسالة غير صالح
    public const string InvalidMessage = "INVALID_MESSAGE";

    // طلب غير صالح
    public const string InvalidRequest = "INVALID_REQUEST";

    // الرسالة غير موجودة أو ليست لك
    public const string MessageNotFound = "MESSAGE_NOT_FOUND";

    // محاولات دخول كثيرة
    public const string TooManyAttempts = "TOO_MANY_ATTEMPTS";

    // قاعدة البيانات غير متاحة
    public const string DatabaseUnavailable = "DATABASE_UNAVAILABLE";
}