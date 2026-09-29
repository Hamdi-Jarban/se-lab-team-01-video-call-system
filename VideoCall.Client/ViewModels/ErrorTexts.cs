using VideoCall.Shared.Messages;

namespace VideoCall.Client.ViewModels;

// تحويل رموز أخطاء الخادم إلى رسائل عربية مفهومة للمستخدم
public static class ErrorTexts
{
    public static string ForCode(string? code, string? serverMessage = null)
    {
        // إن أرسل الخادم نصًا مفصلًا (غير الرمز نفسه) نعرضه كما هو
        if (!string.IsNullOrWhiteSpace(serverMessage) && !string.Equals(serverMessage, code, StringComparison.Ordinal))
            return serverMessage;

        return code switch
        {
            ErrorCodes.UsernameAlreadyExists => "اسم المستخدم مستخدم مسبقًا.",
            ErrorCodes.InvalidUsername => "اسم المستخدم غير صالح (من 3 إلى 32 حرفًا: حروف وأرقام و . _ -).",
            ErrorCodes.WeakPassword => "كلمة المرور يجب ألا تقل عن 6 أحرف.",
            ErrorCodes.InvalidCredentials => "اسم المستخدم أو كلمة المرور غير صحيحة.",
            ErrorCodes.AlreadyLoggedIn => "هذا المستخدم مسجّل الدخول بالفعل.",
            ErrorCodes.ServerUnavailable => "الخادم أو قاعدة البيانات غير متاحة حاليًا.",
            ErrorCodes.DatabaseUnavailable => "تعذر تنفيذ الطلب حاليًا، حاول لاحقًا.",
            ErrorCodes.NotAuthenticated => "يجب تسجيل الدخول أولًا.",
            ErrorCodes.UserNotFound => "المستخدم غير موجود.",
            ErrorCodes.ConversationNotFound => "المحادثة غير موجودة.",
            ErrorCodes.NotConversationMember => "لست عضوًا في هذه المحادثة.",
            ErrorCodes.NotGroupAdmin => "هذه العملية لمدير المجموعة فقط.",
            ErrorCodes.InvalidMessage => "الرسالة فارغة أو طويلة جدًا.",
            ErrorCodes.MessageNotFound => "لا يمكنك تعديل هذه الرسالة أو حذفها.",
            ErrorCodes.TooManyAttempts => "محاولات دخول كثيرة، انتظر 30 ثانية ثم حاول مجددًا.",
            ErrorCodes.InvalidRequest => "الطلب غير صالح.",
            _ => "حدث خطأ غير متوقع."
        };
    }
}
