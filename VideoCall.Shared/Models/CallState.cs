namespace VideoCall.Shared.Models;

// حالات المكالمة المختلفة أثناء دورة الاتصال
public enum CallState
{
    Calling,    // جاري إجراء المكالمة
    Ringing,    // المكالمة ترن لدى الطرف الآخر
    Connected,  // تم قبول المكالمة والاتصال قائم
    Ended,      // انتهت المكالمة
    Rejected,   // تم رفض المكالمة
    TimedOut    // انتهت مهلة انتظار الرد
}

// يمثل جلسة مكالمة فردية بين مستخدمين على الخادم
public class CallSession
{
    // المعرّف الفريد للمكالمة
    public Guid CallId { get; init; } = Guid.NewGuid();

    // اسم المستخدم الذي بدأ المكالمة
    public string Caller { get; init; } = string.Empty;

    // اسم المستخدم المستهدف بالمكالمة
    public string Callee { get; init; } = string.Empty;

    // الحالة الحالية للمكالمة
    public CallState State { get; set; } = CallState.Calling;

    // وقت إنشاء المكالمة بالتوقيت العالمي UTC
    public DateTime CreationTimeUtc { get; init; } = DateTime.UtcNow;
}