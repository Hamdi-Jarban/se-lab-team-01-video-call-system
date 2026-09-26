namespace VideoCall.Shared.Models;

// نوع المحادثة: خاصة بين مستخدمين أو جماعية
public enum ConversationType
{
    Private, // محادثة خاصة
    Group    // محادثة جماعية
}

// الحالات التي يمكن أن تمر بها المحادثة
public enum ConversationState
{
    Created, // تم إنشاء المحادثة
    Active,  // المحادثة نشطة
    Ended    // انتهت المحادثة
}

// يمثل محادثة داخل النظام
public sealed class Conversation
{
    // المعرّف الخاص بالمحادثة
    public string Id { get; init; } = string.Empty;

    // نوع المحادثة
    public ConversationType Type { get; init; }

    // اسم المستخدم المسؤول عن المحادثة
    public string Host { get; set; } = string.Empty;

    // مجموعة أعضاء المحادثة مع تجاهل اختلاف حالة الأحرف
    public HashSet<string> Members { get; } = new(StringComparer.OrdinalIgnoreCase);

    // الحالة الحالية للمحادثة
    public ConversationState State { get; set; } = ConversationState.Created;

    // معرّف الوسائط المرتبطة بالمحادثة إن وجد
    public Guid? MediaId { get; set; }
}